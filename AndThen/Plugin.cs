using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using AndThen.Windows;

namespace AndThen;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    public const string AppVersion = "0.0.1.3";
    private const string CommandName = "/andthen";
    private const string CommandAlias = "/atn";

    internal static Plugin Instance { get; private set; } = null!;
    public Configuration Configuration { get; }
    public readonly WindowSystem WindowSystem = new("AndThen");
    internal Engine Engine { get; }

    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;
    private readonly DialogWindow dialogWindow;
    private bool paused;
    private DateTime? pauseUntil;

    public Plugin()
    {
        Instance = this;
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Configuration.PollSec <= 0) Configuration.PollSec = 0.5f;
        Engine = new Engine(Configuration);

        mainWindow = new MainWindow(this);
        configWindow = new ConfigWindow(this);
        dialogWindow = new DialogWindow(this);
        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);
        WindowSystem.AddWindow(dialogWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = "AndThen. /atn help" });
        try { CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand) { HelpMessage = "Alias for /andthen." }); }
        catch (Exception ex) { Log.Verbose(ex, "Could not register /atn"); }

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        Framework.Update += OnFramework;
        ClientState.TerritoryChanged += OnTerritory;
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;

        if (Configuration.OpenUiOnLoad) mainWindow.IsOpen = true;
    }

    public void Dispose()
    {
        Framework.Update -= OnFramework;
        ClientState.TerritoryChanged -= OnTerritory;
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        WindowSystem.RemoveAllWindows();
        try { CommandManager.RemoveHandler(CommandName); } catch { /* gone */ }
        try { CommandManager.RemoveHandler(CommandAlias); } catch { /* gone */ }
    }

    public void ToggleConfigUi() => configWindow.Toggle();
    public void ToggleMainUi() => mainWindow.Toggle();
    public void OpenAsk() { dialogWindow.IsOpen = true; }
    public GameSnapshot Snapshot() => Engine.LastSnap.LoggedIn ? Engine.LastSnap : GameSnapshot.Capture();
    public System.Collections.Generic.IReadOnlyList<ThenRule> CurrentMatches() => Engine.LastMatches;
    public static bool CharacterReady =>
        ClientState.IsLoggedIn && ObjectTable.LocalPlayer is not null && PlayerState.IsLoaded;
    public bool IsPaused => paused;

    public static void Notify(string text)
    {
        if (Instance is { Configuration.NotifyInChat: false }) return;
        try { Chat.Print("[AndThen] " + text); }
        catch { /* chat not ready */ }
    }

    public void ApplyNow()
    {
        if (paused) { Notify("Paused."); return; }
        Engine.Tick(true);
    }

    public void TestRule(ThenRule rule)
    {
        if (!CharacterReady) { Notify("Not logged in."); return; }
        Engine.Test(rule);
    }

    public ThenRule? FindRule(string name) =>
        Configuration.Rules.FirstOrDefault(r => r.CommandToken.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    public void DuplicateRule(ThenRule rule)
    {
        var copy = Share.TryDecode(Share.ToJson(rule), out var decoded, out _) ? decoded : null;
        if (copy is null) return;
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = rule.Name + " (copy)";
        copy.Enabled = false;
        copy.Mode = ApplyMode.Off;
        copy.Folder = rule.Folder;
        Configuration.Rules.Add(copy);
        Configuration.Save();
        mainWindow.OpenRule(copy.Id);
    }

    public bool TryImport(string text, out string error) => TryImportMany(text, false, out error);

    public bool TryImportMany(string text, bool replace, out string error)
    {
        if (!Share.TryDecodeMany(text, out var many, out error) || many.Count == 0) return false;
        if (replace) Configuration.Rules.Clear();
        ThenRule? last = null;
        foreach (var rule in many)
        {
            rule.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(rule.Name)) rule.Name = "Imported rule";
            else if (!replace) rule.Name += " (copy)";
            rule.Enabled = false;
            rule.Mode = ApplyMode.Off;
            Configuration.Rules.Add(rule);
            last = rule;
        }
        Configuration.Save();
        if (last is not null) mainWindow.OpenRule(last.Id);
        error = string.Empty;
        return true;
    }

    public void MoveRule(ThenRule rule, int delta)
    {
        var i = Configuration.Rules.IndexOf(rule);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Configuration.Rules.Count) return;
        Configuration.Rules.RemoveAt(i);
        Configuration.Rules.Insert(j, rule);
        Configuration.Save();
    }

    private void OnFramework(IFramework _)
    {
        if (paused && pauseUntil is DateTime until && DateTime.Now >= until)
        {
            paused = false;
            pauseUntil = null;
            Notify("Pause ended.");
        }
        if (!CharacterReady) { Engine.Reset(); return; }
        if (paused) return;
        Engine.Tick(false);
        if (Engine.DialogOpenNeeded)
        {
            Engine.DialogOpenNeeded = false;
            dialogWindow.IsOpen = true;
        }
    }

    private void OnTerritory(uint _) => Engine.Tick(false);
    private void OnLogin() => Engine.Reset();
    private void OnLogout(int type, int code) => Engine.Reset();

    private void OnCommand(string command, string args)
    {
        var text = (args ?? string.Empty).Trim();
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var key = parts.Length == 0 ? string.Empty : parts[0].ToLowerInvariant();
        switch (key)
        {
            case "help":
                Notify("/atn — window");
                Notify("/atn Name — run that rule");
                Notify("/atn ask — show dialog list");
                Notify("/atn why Name — print chip results");
                Notify("/atn apply | now | pause | resume | zone | config");
                break;
            case "config": ToggleConfigUi(); break;
            case "apply":
            case "update": ApplyNow(); break;
            case "now":
                Engine.Tick(false);
                if (Engine.LastMatches.Count == 0) Notify("No matching rule.");
                else foreach (var rule in Engine.LastMatches) Notify($"Match [{rule.Name}]");
                break;
            case "ask": OpenAsk(); break;
            case "pause":
                paused = true;
                if (parts.Length > 1 && int.TryParse(parts[1], out var secs) && secs > 0)
                {
                    pauseUntil = DateTime.Now.AddSeconds(secs);
                    Notify($"Paused {secs}s.");
                }
                else { pauseUntil = null; Notify("Paused."); }
                break;
            case "resume":
                paused = false;
                pauseUntil = null;
                Notify("Resumed.");
                Engine.Tick(true);
                break;
            case "zone": Notify(Snapshot().Line()); break;
            case "why":
            {
                var name = parts.Length > 1 ? parts[1] : string.Empty;
                var rule = string.IsNullOrWhiteSpace(name) ? null : FindRule(name);
                if (rule is null) Notify("Name a rule: /atn why RuleName");
                else foreach (var line in Engine.Why(rule, Snapshot())) Notify(line);
                break;
            }
            case "dry":
            {
                var name = parts.Length > 1 ? parts[1] : string.Empty;
                var rule = string.IsNullOrWhiteSpace(name) ? null : FindRule(name);
                if (rule is null) Notify("Name a rule: /atn dry RuleName");
                else foreach (var line in Engine.Preview(rule)) Notify(line);
                break;
            }
            case "": ToggleMainUi(); break;
            default:
            {
                var rule = FindRule(text);
                if (rule is null) { Notify($"No rule named {text}"); ToggleMainUi(); break; }
                TestRule(rule);
                break;
            }
        }
    }
}
