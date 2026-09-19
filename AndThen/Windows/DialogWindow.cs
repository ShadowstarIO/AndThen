using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AndThen.Windows;

public sealed class DialogWindow : Window
{
    private readonly Plugin plugin;

    public DialogWindow(Plugin plugin) : base("AndThen · Apply?###AndThenAsk")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 180),
            MaximumSize = new Vector2(720, 640),
        };
    }

    public override void Draw()
    {
        var list = plugin.Engine.DialogQueue;
        if (list.Count == 0)
        {
            ImGui.TextDisabled("Nothing waiting.");
            if (ImGui.Button("Close")) IsOpen = false;
            return;
        }

        ImGui.TextWrapped("Conditions match. Click a rule to apply it.");
        ImGui.Separator();
        ThenRule? pick = null;
        foreach (var rule in list)
        {
            ImGui.PushID(rule.Id);
            if (ImGui.Button(rule.Name, new Vector2(-1, 0))) pick = rule;
            if (!string.IsNullOrWhiteSpace(rule.Notes))
                ImGui.TextWrapped(rule.Notes);
            ImGui.PopID();
        }
        ImGui.Separator();
        if (ImGui.Button("Dismiss"))
        {
            plugin.Engine.DismissDialog();
            IsOpen = false;
        }
        if (pick is not null)
        {
            plugin.Engine.AcceptDialog(pick);
            IsOpen = false;
        }
    }
}
