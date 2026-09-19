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
            MinimumSize = new Vector2(360, 220),
            MaximumSize = new Vector2(640, 520),
        };
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            cfg.Enabled = enabled;
            cfg.Save();
        }

        var notify = cfg.NotifyInChat;
        if (ImGui.Checkbox("Chat notices", ref notify))
        {
            cfg.NotifyInChat = notify;
            cfg.Save();
        }

        var open = cfg.OpenUiOnLoad;
        if (ImGui.Checkbox("Open on login", ref open))
        {
            cfg.OpenUiOnLoad = open;
            cfg.Save();
        }

        var poll = cfg.PollMs;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Check interval (ms)", ref poll))
        {
            cfg.PollMs = Math.Clamp(poll, 100, 5000);
            cfg.Save();
        }

        ImGui.Separator();
        ImGui.TextWrapped("Rules fire once when they become true. They do not revert. Add another rule for the other state.");
        ImGui.TextDisabled($"Version {Plugin.AppVersion}");
    }
}
