using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AndThen.Windows;

public sealed partial class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string newRuleName = string.Empty;
    private string newFolder = string.Empty;
    private string filter = string.Empty;
    private string selectedFolder = "All";
    private string? selectedRuleId;
    private string importMsg = string.Empty;
    private string chipValue = string.Empty;
    private string thenValue = string.Empty;
    private string optionQuery = string.Empty;
    private int chipKind;
    private int thenKind;
    private int configSection;
    private int waitMs = 250;
    private string pickedOption = string.Empty;
    private string? dragRuleId;
    private readonly HashSet<string> openFolders = new(StringComparer.OrdinalIgnoreCase) { "Examples" };

    public MainWindow(Plugin plugin) : base($"AndThen v{Plugin.AppVersion}###AndThenMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(920, 560), MaximumSize = new Vector2(1800, 1400) };
    }

    public void Dispose() { }
    public void OpenRule(string id) { selectedRuleId = id; IsOpen = true; }

    public override void Draw()
    {
        WindowName = $"AndThen v{Plugin.AppVersion}###AndThenMain";
        var cfg = plugin.Configuration;
        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) { cfg.Enabled = enabled; cfg.Save(); }
        ImGui.SameLine();
        if (ImGui.Button("Apply now")) plugin.ApplyNow();
        ImGui.SameLine();
        if (ImGui.Button("Ask")) plugin.OpenAsk();
        ImGui.SameLine();
        if (ImGui.Button("Settings")) plugin.ToggleConfigUi();
        ImGui.SameLine();
        ImGui.TextDisabled(plugin.IsPaused ? "Paused" : plugin.Snapshot().Line());
        var matches = plugin.CurrentMatches();
        ImGui.TextUnformatted(matches.Count == 0 ? "Match: none" : "Match:");
        foreach (var rule in matches.Take(8)) { ImGui.SameLine(); if (ImGui.SmallButton(rule.Name)) OpenRule(rule.Id); }
        ImGui.SetNextItemWidth(220);
        ImGui.InputTextWithHint("##filter", "Search name, note, chips", ref filter, 80);
        ImGui.Separator();
        var avail = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("left", new Vector2(Math.Min(340, avail.X * 0.36f), 0), true);
        DrawTree(cfg);
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("right", new Vector2(0, 0), true);
        var selected = cfg.Rules.Find(r => r.Id == selectedRuleId);
        if (selected is null) ImGui.TextDisabled("Select a rule.");
        else DrawEditor(cfg, selected);
        ImGui.EndChild();
    }

    private void DrawTree(Configuration cfg)
    {
        if (ImGui.Selectable("All", selectedFolder == "All")) selectedFolder = "All";
        DropFolder(cfg, string.Empty);
        if (ImGui.Selectable("Ungrouped", selectedFolder == "Ungrouped")) selectedFolder = "Ungrouped";
        DropFolder(cfg, string.Empty);
        var folders = cfg.Folders.Concat(cfg.Rules.Select(r => r.FolderKey)).Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var folder in folders) DrawFolder(cfg, folder);
        ImGui.Separator();
        ThenRule? remove = null;
        if (selectedFolder is "All" or "Ungrouped")
            foreach (var rule in cfg.Rules.Where(r => Visible(r) && (selectedFolder == "All" || string.IsNullOrWhiteSpace(r.Folder))))
                if (DrawRuleRow(cfg, rule)) remove = rule;
        ImGui.Separator();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##nf", "New folder", ref newFolder, 40);
        if (ImGui.Button("Add folder") && !string.IsNullOrWhiteSpace(newFolder))
        {
            if (!cfg.Folders.Contains(newFolder.Trim(), StringComparer.OrdinalIgnoreCase)) cfg.Folders.Add(newFolder.Trim());
            selectedFolder = newFolder.Trim();
            openFolders.Add(selectedFolder);
            newFolder = string.Empty;
            cfg.Save();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140);
        ImGui.InputTextWithHint("##nr", "New rule", ref newRuleName, 64);
        if (ImGui.Button("Add rule"))
        {
            var rule = new ThenRule
            {
                Name = string.IsNullOrWhiteSpace(newRuleName) ? "New rule" : newRuleName.Trim(),
                Enabled = true, Mode = ApplyMode.Off,
                Folder = selectedFolder is "All" or "Ungrouped" ? string.Empty : selectedFolder,
            };
            cfg.Rules.Add(rule);
            OpenRule(rule.Id);
            newRuleName = string.Empty;
            cfg.Save();
        }
        if (ImGui.Button("Import"))
            importMsg = plugin.TryImport(ImGui.GetClipboardText() ?? string.Empty, out var err) ? "Imported." : err;
        if (!string.IsNullOrEmpty(importMsg)) { ImGui.SameLine(); ImGui.TextDisabled(importMsg); }
        if (remove is not null)
        {
            cfg.Rules.Remove(remove);
            if (selectedRuleId == remove.Id) selectedRuleId = null;
            cfg.Save();
        }
    }

    private void DrawFolder(Configuration cfg, string folder)
    {
        var open = openFolders.Contains(folder);
        if (ImGui.SmallButton(open ? "-" : "+")) { if (open) openFolders.Remove(folder); else openFolders.Add(folder); }
        ImGui.SameLine();
        var mute = cfg.MutedFolders.Contains(folder);
        if (ImGui.Checkbox($"##m{folder}", ref mute))
        {
            if (mute && !cfg.MutedFolders.Contains(folder)) cfg.MutedFolders.Add(folder);
            if (!mute) cfg.MutedFolders.RemoveAll(f => f.Equals(folder, StringComparison.OrdinalIgnoreCase));
            cfg.Save();
        }
        ImGui.SameLine();
        if (ImGui.Selectable(folder, selectedFolder.Equals(folder, StringComparison.OrdinalIgnoreCase))) selectedFolder = folder;
        DropFolder(cfg, folder);
        if (!openFolders.Contains(folder)) return;
        ImGui.Indent();
        ThenRule? remove = null;
        foreach (var rule in cfg.Rules.Where(r => r.FolderKey.Equals(folder, StringComparison.OrdinalIgnoreCase) && Visible(r)))
            if (DrawRuleRow(cfg, rule)) remove = rule;
        ImGui.Unindent();
        if (remove is not null)
        {
            cfg.Rules.Remove(remove);
            if (selectedRuleId == remove.Id) selectedRuleId = null;
            cfg.Save();
        }
    }

    private bool DrawRuleRow(Configuration cfg, ThenRule rule)
    {
        ImGui.PushID(rule.Id);
        Led(rule);
        ImGui.SameLine();
        if (ImGui.Selectable(string.IsNullOrWhiteSpace(rule.Notes) ? rule.Name : $"{rule.Name}  ", selectedRuleId == rule.Id))
            selectedRuleId = rule.Id;
        if (!string.IsNullOrWhiteSpace(rule.Notes)) { ImGui.SameLine(); ImGui.TextDisabled(TrimOne(rule.Notes, 28)); }
        if (ImGui.BeginDragDropSource())
        {
            dragRuleId = rule.Id;
            ImGui.SetDragDropPayload("ANDTHEN_RULE", Encoding.UTF8.GetBytes(rule.Id));
            ImGui.TextUnformatted(rule.Name);
            ImGui.EndDragDropSource();
        }
        var kill = false;
        if (ImGui.BeginPopupContextItem("ctx"))
        {
            if (ImGui.MenuItem("Move down")) plugin.MoveRule(rule, 1);
            if (ImGui.MenuItem("Move up")) plugin.MoveRule(rule, -1);
            if (ImGui.MenuItem("Duplicate")) plugin.DuplicateRule(rule);
            if (ImGui.MenuItem("Copy JSON")) { ImGui.SetClipboardText(Share.ToJson(rule)); importMsg = "Copied JSON."; }
            if (ImGui.MenuItem("Copy AT1")) { ImGui.SetClipboardText(Share.Encode(rule)); importMsg = "Copied share code."; }
            if (ImGui.MenuItem("Run THEN")) plugin.TestRule(rule);
            if (ImGui.MenuItem("Why")) foreach (var line in plugin.Engine.Why(rule, plugin.Snapshot())) Plugin.Notify(line);
            if (ImGui.GetIO().KeyShift) { if (ImGui.MenuItem("Delete")) kill = true; }
            else ImGui.MenuItem("Delete (hold Shift)", false);
            ImGui.EndPopup();
        }
        ImGui.PopID();
        return kill;
    }

    private void DropFolder(Configuration cfg, string folder)
    {
        if (!ImGui.BeginDragDropTarget()) return;
        var payload = ImGui.AcceptDragDropPayload("ANDTHEN_RULE");
        if (!payload.IsNull && payload.DataSize > 0)
        {
            var id = dragRuleId ?? string.Empty;
            if (id.Length == 0)
            {
                unsafe { id = Encoding.UTF8.GetString((byte*)payload.Data, payload.DataSize); }
            }
            var rule = cfg.Rules.Find(r => r.Id == id);
            if (rule is not null) { rule.Folder = folder; cfg.Save(); }
        }
        ImGui.EndDragDropTarget();
    }

    private void Led(ThenRule rule)
    {
        Vector4 color = !rule.Enabled ? new Vector4(0.35f, 0.35f, 0.35f, 1)
            : plugin.Engine.JustApplied(rule.Id) ? new Vector4(0.92f, 0.62f, 0.16f, 1)
            : plugin.Engine.LastMatches.Any(r => r.Id == rule.Id) ? new Vector4(0.17f, 0.70f, 0.69f, 1)
            : new Vector4(0.55f, 0.55f, 0.55f, 1);
        ImGui.ColorButton("##led", color, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop, new Vector2(10, 10));
    }

    private bool Visible(ThenRule rule)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        if (rule.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
        if ((rule.Notes ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
        if ((rule.Folder ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var chip in rule.AndChips.Concat(rule.OrChips).Concat(rule.NotChips))
            if (chip.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var row in rule.Then)
            if (row.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
