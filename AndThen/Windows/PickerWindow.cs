using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AndThen.Windows;

public sealed class PickerWindow : Window
{
    private string query = string.Empty;
    private string[] items = [];
    private Action<string>? pick;
    private string heading = "Pick";
    private string current = string.Empty;

    public PickerWindow() : base("AndThen · Pick###AndThenPick")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 360),
            MaximumSize = new Vector2(640, 900),
        };
    }

    public void Open(string title, string[] list, Action<string> onPick, string currentValue = "")
    {
        heading = title;
        items = list;
        pick = onPick;
        current = currentValue ?? string.Empty;
        query = string.Empty;
        WindowName = $"{title}###AndThenPick";
        IsOpen = true;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(heading);
        if (!string.IsNullOrEmpty(current) && ImGui.Button("current: " + current + " [+]"))
        {
            pick?.Invoke(current);
            IsOpen = false;
        }
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##q", "Search", ref query, 80);
        ImGui.BeginChild("list", new Vector2(0, -32), true);
        var hits = string.IsNullOrWhiteSpace(query)
            ? items
            : items.Where(i => i.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var item in hits.Take(500))
        {
            if (ImGui.Selectable(item, item.Equals(current, StringComparison.OrdinalIgnoreCase)))
            {
                pick?.Invoke(item);
                IsOpen = false;
            }
        }
        if (hits.Length == 0) ImGui.TextDisabled("No matches.");
        ImGui.EndChild();
        if (ImGui.Button("Close", new Vector2(-1, 0))) IsOpen = false;
    }
}
