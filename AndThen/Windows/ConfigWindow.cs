using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AndThen.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("AndThen · Settings###AndThenConfig")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 280),
            MaximumSize = new Vector2(640, 720),
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

        var quiet = cfg.QuietInCutscene;
        if (ImGui.Checkbox("Do not Auto during cutscenes", ref quiet)) { cfg.QuietInCutscene = quiet; cfg.Save(); }

        var poll = cfg.PollSec;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputFloat("Check interval (seconds)", ref poll, 0.25f, 0.5f, "%.2f"))
        {
            cfg.PollSec = Math.Clamp(poll, 0.25f, 5f);
            cfg.Save();
        }

        ImGui.Separator();
        ImGui.TextWrapped("Off does nothing until /atn Name. Dialog asks once per match set. Auto runs on the rising edge.");
        ImGui.TextDisabled($"Version {Plugin.AppVersion}");
        ImGui.Separator();
        ImGui.TextUnformatted("Last applied");
        if (plugin.Engine.Log.Count == 0) ImGui.TextDisabled("None yet.");
        foreach (var line in plugin.Engine.Log)
            ImGui.TextUnformatted($"{line.At:HH:mm:ss}  {line.Name}  {line.Detail}");
    }
}
