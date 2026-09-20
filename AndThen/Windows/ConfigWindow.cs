using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AndThen.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private string backupMsg = string.Empty;

    public ConfigWindow(Plugin plugin) : base("AndThen · Settings###AndThenConfig")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 420),
            MaximumSize = new Vector2(720, 900),
        };
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) { cfg.Enabled = enabled; cfg.Save(); }

        var notify = cfg.NotifyInChat;
        if (ImGui.Checkbox("Chat notices", ref notify)) { cfg.NotifyInChat = notify; cfg.Save(); }

        var open = cfg.OpenUiOnLoad;
        if (ImGui.Checkbox("Open on login", ref open)) { cfg.OpenUiOnLoad = open; cfg.Save(); }

        var poll = cfg.PollSec;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputFloat("Check interval (seconds)", ref poll, 0.25f, 0.5f, "%.2f"))
        {
            cfg.PollSec = Math.Clamp(poll, 0.25f, 5f);
            cfg.Save();
        }

        var delay = cfg.DefaultDelaySec;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputFloat("Default wait (seconds)", ref delay, 0.5f, 1f, "%.1f"))
        {
            cfg.DefaultDelaySec = Math.Clamp(delay, 0f, 120f);
            cfg.Save();
        }
        ImGui.TextDisabled("New rules copy this wait. 0 = fire as soon as the condition is true.");

        ImGui.Separator();
        ImGui.TextUnformatted("Do not Auto or Dialog while");
        Quiet(cfg, "Cutscenes", () => cfg.QuietInCutscene, v => cfg.QuietInCutscene = v);
        Quiet(cfg, "Combat", () => cfg.QuietInCombat, v => cfg.QuietInCombat = v);
        Quiet(cfg, "Inside a duty", () => cfg.QuietInDuty, v => cfg.QuietInDuty = v);
        Quiet(cfg, "Between areas", () => cfg.QuietBetweenAreas, v => cfg.QuietBetweenAreas = v);
        Quiet(cfg, "Occupied (event, NPC, item)", () => cfg.QuietWhenOccupied, v => cfg.QuietWhenOccupied = v);
        Quiet(cfg, "GPose", () => cfg.QuietInGPose, v => cfg.QuietInGPose = v);
        Quiet(cfg, "Dead", () => cfg.QuietWhenDead, v => cfg.QuietWhenDead = v);
        Quiet(cfg, "Crafting", () => cfg.QuietWhenCrafting, v => cfg.QuietWhenCrafting = v);
        Quiet(cfg, "Performing", () => cfg.QuietWhenPerforming, v => cfg.QuietWhenPerforming = v);
        Quiet(cfg, "Trade window", () => cfg.QuietWhenTrading, v => cfg.QuietWhenTrading = v);
        ImGui.TextDisabled("/atn Name and Test still run.");

        ImGui.Separator();
        ImGui.TextUnformatted("Backup");
        ImGui.TextWrapped("Copy every rule as JSON or AT1. Paste restores them as new copies (Off). Hold Shift to replace everything.");
        if (ImGui.Button("Copy all JSON"))
        {
            ImGui.SetClipboardText(Share.ToJsonAll(cfg.Rules));
            backupMsg = $"Copied {cfg.Rules.Count} rule(s) as JSON.";
        }
        ImGui.SameLine();
        if (ImGui.Button("Copy all AT1"))
        {
            ImGui.SetClipboardText(string.Join('\n', cfg.Rules.ConvertAll(Share.Encode)));
            backupMsg = $"Copied {cfg.Rules.Count} share code(s).";
        }
        if (ImGui.Button("Paste add"))
            backupMsg = plugin.TryImportMany(ImGui.GetClipboardText() ?? string.Empty, false, out var addErr) ? "Imported." : addErr;
        ImGui.SameLine();
        if (ImGui.GetIO().KeyShift)
        {
            if (ImGui.Button("Paste replace"))
                backupMsg = plugin.TryImportMany(ImGui.GetClipboardText() ?? string.Empty, true, out var repErr) ? "Replaced." : repErr;
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("Paste replace");
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Hold Shift. Replaces every rule.");
        }
        if (!string.IsNullOrEmpty(backupMsg)) ImGui.TextWrapped(backupMsg);

        ImGui.Separator();
        ImGui.TextWrapped("Off waits for /atn Name. Dialog asks once per match set. Auto runs on the rising edge. Rules do not revert.");
        ImGui.TextDisabled($"Version {Plugin.AppVersion}");
        ImGui.Separator();
        ImGui.TextUnformatted("Now");
        ImGui.TextWrapped(plugin.Snapshot().Line());
        ImGui.Separator();
        ImGui.TextUnformatted("Last applied");
        if (plugin.Engine.Log.Count == 0) ImGui.TextDisabled("None yet.");
        foreach (var line in plugin.Engine.Log)
            ImGui.TextUnformatted($"{line.At:HH:mm:ss}  {line.Name}  {line.Detail}");
    }

    private static void Quiet(Configuration cfg, string label, Func<bool> get, Action<bool> set)
    {
        var v = get();
        if (ImGui.Checkbox(label, ref v)) { set(v); cfg.Save(); }
    }
}
