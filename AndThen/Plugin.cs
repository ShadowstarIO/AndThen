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
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    public const string AppVersion = "0.0.1.0";
    private const string CommandName = "/andthen";
    private const string CommandAlias = "/atn";

    internal static Plugin Instance { get; private set; } = null!;
    public Configuration Configuration { get; }
    public readonly WindowSystem WindowSystem = new("AndThen");

    private readonly Engine engine;
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;
    private bool paused;
    private DateTime? pauseUntil;

    public Plugin()
    {
        Instance = this;
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        engine = new Engine(Configuration);

        mainWindow = new MainWindow(this);
        configWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "AndThen. /atn help",
        });
        try
        {
            CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
            {
                HelpMessage = "Alias for /andthen.",
            });
        }
        catch (Exception ex)
        {
            Log.Verbose(ex, "Could not register /atn");
        }

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        Framework.Update += OnFramework;
        ClientState.TerritoryChanged += OnTerritory;
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;

        if (Configuration.OpenUiOnLoad)
            mainWindow.IsOpen = true;
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
    public GameSnapshot Snapshot() => engine.LastSnap.LoggedIn ? engine.LastSnap : GameSnapshot.Capture();
    public System.Collections.Generic.IReadOnlyList<ThenRule> CurrentMatches() => engine.LastMatches;
    public static bool CharacterReady =>
        ClientState.IsLoggedIn && ObjectTable.LocalPlayer is not null && PlayerState.IsLoaded;
    public bool IsPaused => paused;

    public static void Notify(string text)
    {
        if (Instance is { Configuration.NotifyInChat: false }) return;
        try { Chat.Print("[AndThen] " + text); }
        catch { /* chat not ready */ }
    }

    public void RequestEval() => engine.Tick(false);

    public void ApplyNow()
    {
        if (paused)
        {
            Notify("Paused.");
            return;
        }
        engine.Tick(true);
    }

    public void TestRule(ThenRule rule)
    {
        if (!CharacterReady)
        {
            Notify("Not logged in.");
            return;
        }
        var snap = GameSnapshot.Capture();
        Notify(ChipEval.Matches(rule, snap)
            ? $"[{rule.Name}] matches now. Running THEN."
            : $"[{rule.Name}] does not match now. Running THEN anyway.");
        engine.Test(rule);
    }

    public void MovePriority(ThenRule rule, int delta)
    {
        var ordered = Configuration.Rules.OrderBy(r => r.Priority).ToList();
        var i = ordered.FindIndex(r => r.Id == rule.Id);
        var j = i + (delta > 0 ? 1 : -1);
        if (i < 0 || j < 0 || j >= ordered.Count) return;
        (ordered[i].Priority, ordered[j].Priority) = (ordered[j].Priority, ordered[i].Priority);
        Configuration.Save();
    }

    public void DuplicateRule(ThenRule rule)
    {
        var copy = Share.TryDecode(Share.ToJson(rule), out var decoded, out _) ? decoded : null;
        if (copy is null) return;
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = rule.Name + " (copy)";
        copy.Priority = Configuration.Rules.Count == 0 ? 10 : Configuration.Rules.Max(r => r.Priority) + 10;
        copy.Enabled = false;
        Configuration.Rules.Add(copy);
        Configuration.Save();
        mainWindow.OpenRule(copy.Id);
    }

    public bool TryImport(string text, out string error)
    {
        if (!Share.TryDecode(text, out var rule, out error) || rule is null) return false;
        rule.Id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(rule.Name)) rule.Name = "Imported rule";
        else rule.Name += " (copy)";
        rule.Enabled = false;
        Configuration.Rules.Add(rule);
        Configuration.Save();
        mainWindow.OpenRule(rule.Id);
        error = string.Empty;
        return true;
    }

    private void OnFramework(IFramework _)
    {
        if (paused && pauseUntil is DateTime until && DateTime.Now >= until)
        {
            paused = false;
            pauseUntil = null;
            Notify("Pause ended.");
        }
        if (!CharacterReady)
        {
            engine.Reset();
            return;
        }
        if (paused) return;
        engine.Tick(false);
    }

    private void OnTerritory(ushort _) => engine.Tick(false);
    private void OnLogin() => engine.Reset();
    private void OnLogout(int type, int code) => engine.Reset();

    private void OnCommand(string command, string args)
    {
        var parts = (args ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var key = parts.Length == 0 ? string.Empty : parts[0].ToLowerInvariant();
        switch (key)
        {
            case "help":
                Notify("/atn — window");
                Notify("/atn apply — run matching rules now");
                Notify("/atn now — preview matches");
                Notify("/atn pause [seconds]");
                Notify("/atn resume");
                Notify("/atn zone");
                Notify("/atn config");
                break;
            case "config":
                ToggleConfigUi();
                break;
            case "apply":
            case "update":
                ApplyNow();
                break;
            case "now":
            {
                engine.Tick(false);
                if (engine.LastMatches.Count == 0) Notify("No matching rule.");
                else foreach (var rule in engine.LastMatches)
                    Notify($"Match P{rule.Priority} [{rule.Name}]");
                break;
            }
            case "pause":
                paused = true;
                if (parts.Length > 1 && int.TryParse(parts[1], out var secs) && secs > 0)
                {
                    pauseUntil = DateTime.Now.AddSeconds(secs);
                    Notify($"Paused {secs}s.");
                }
                else
                {
                    pauseUntil = null;
                    Notify("Paused.");
                }
                break;
            case "resume":
                paused = false;
                pauseUntil = null;
                Notify("Resumed.");
                engine.Tick(true);
                break;
            case "zone":
                Notify(Snapshot().Line());
                break;
            default:
                ToggleMainUi();
                break;
        }
    }
}
