using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace AndThen.Windows;

public sealed partial class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string newName = string.Empty;
    private string filter = string.Empty;
    private string? selectedRuleId;
    private string selectedFolder = string.Empty;
    private string importMsg = string.Empty;
    private string chipValue = string.Empty;
    private string thenValue = string.Empty;
    private string optionQuery = string.Empty;
    private string pickedOption = string.Empty;
    private string configGroup = "Graphics";
    private string partyCmp = ">=";
    private int partyN = 4;
    private int chipKind;
    private int thenKind;
    private int lastChipKind = -1;
    private int lastThenKind = -1;
    private int configSection;
    private int waitMs = 250;
    private int optionPick;
    private string? dragRuleId;
    private readonly HashSet<string> openFolders = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow(Plugin plugin) : base($"AndThen v{Plugin.AppVersion}###AndThenMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(960, 560), MaximumSize = new Vector2(1800, 1400) };
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
        if (IconButton(FontAwesomeIcon.Play, "apply", "Apply matching rules now")) plugin.ApplyNow();
        ImGui.SameLine();
        if (IconButton(FontAwesomeIcon.Comment, "ask", "Open Dialog list")) plugin.OpenAsk();
        ImGui.SameLine();
        if (IconButton(FontAwesomeIcon.Cog, "cfg", "Settings")) plugin.ToggleConfigUi();
        ImGui.SameLine();
        ImGui.TextDisabled(plugin.IsPaused ? "Paused" : plugin.Snapshot().Line());
        if (!string.IsNullOrEmpty(importMsg)) { ImGui.SameLine(); ImGui.TextDisabled(importMsg); }
        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        var leftW = Math.Clamp(avail.X * 0.30f, 240, 360);
        ImGui.BeginChild("left", new Vector2(leftW, avail.Y), true);
        DrawSelector(cfg);
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("right", new Vector2(0, avail.Y), true);
        var selected = cfg.Rules.Find(r => r.Id == selectedRuleId);
        if (selected is null) ImGui.TextDisabled("Select a rule, or add one with +.");
        else DrawEditor(cfg, selected);
        ImGui.EndChild();
    }

    private void DrawSelector(Configuration cfg)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##filter", "Search", ref filter, 80);
        var style = ImGui.GetStyle();
        var footerH = ImGui.GetFrameHeightWithSpacing() * 2 + style.ItemSpacing.Y + style.WindowPadding.Y;
        var remain = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("tree", new Vector2(remain.X, Math.Max(40, remain.Y - footerH)), false);
        DrawTree(cfg);
        ImGui.EndChild();
        DrawBottomBar(cfg);
    }

    private void DrawTree(Configuration cfg)
    {
        var folders = cfg.Folders
            .Concat(cfg.Rules.Select(r => r.FolderKey))
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        DrawFolderBranch(cfg, string.Empty, folders);
        ThenRule? remove = null;
        foreach (var rule in cfg.Rules.Where(r => string.IsNullOrWhiteSpace(r.Folder) && Visible(r)))
            if (DrawRuleRow(cfg, rule)) remove = rule;
        DropFolder(cfg, string.Empty);
        if (remove is not null)
        {
            cfg.Rules.Remove(remove);
            if (selectedRuleId == remove.Id) selectedRuleId = null;
            cfg.Save();
        }
    }

    private void DrawFolderBranch(Configuration cfg, string parent, List<string> all)
    {
        var depth = parent.Length == 0 ? 0 : parent.Count(c => c == '/') + 1;
        foreach (var folder in all.Where(f => FolderParent(f) == parent))
        {
            ImGui.PushID(folder);
            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth;
            if (selectedFolder.Equals(folder, StringComparison.OrdinalIgnoreCase))
                flags |= ImGuiTreeNodeFlags.Selected;
            if (openFolders.Contains(folder) || depth == 0) flags |= ImGuiTreeNodeFlags.DefaultOpen;
            var open = ImGui.TreeNodeEx(FolderLeaf(folder) + "###f" + folder, flags);
            if (ImGui.IsItemClicked()) selectedFolder = folder;
            DropFolder(cfg, folder);
            FolderMenu(cfg, folder);
            if (open)
            {
                openFolders.Add(folder);
                DrawFolderBranch(cfg, folder, all);
                ThenRule? kill = null;
                foreach (var rule in cfg.Rules.Where(r => r.FolderKey.Equals(folder, StringComparison.OrdinalIgnoreCase) && Visible(r)))
                    if (DrawRuleRow(cfg, rule)) kill = rule;
                if (kill is not null)
                {
                    cfg.Rules.Remove(kill);
                    if (selectedRuleId == kill.Id) selectedRuleId = null;
                    cfg.Save();
                }
                ImGui.TreePop();
            }
            else openFolders.Remove(folder);
            ImGui.PopID();
        }
    }

    private void FolderMenu(Configuration cfg, string folder)
    {
        if (!ImGui.BeginPopupContextItem("folder")) return;
        if (ImGui.MenuItem("New rule here")) AddRule(cfg, folder);
        if (ImGui.MenuItem("Copy folder JSON"))
        {
            var list = cfg.Rules.Where(r => r.FolderKey.Equals(folder, StringComparison.OrdinalIgnoreCase)).ToList();
            ImGui.SetClipboardText(Share.ToJsonAll(list));
            importMsg = "Copied folder JSON.";
        }
        if (ImGui.GetIO().KeyShift)
        {
            if (ImGui.MenuItem("Delete folder"))
            {
                foreach (var rule in cfg.Rules.Where(r => r.FolderKey.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                    rule.Folder = string.Empty;
                cfg.Folders.RemoveAll(f => f.Equals(folder, StringComparison.OrdinalIgnoreCase));
                if (selectedFolder == folder) selectedFolder = string.Empty;
                cfg.Save();
            }
        }
        else ImGui.MenuItem("Delete folder (hold Shift)", false);
        ImGui.EndPopup();
    }

    private bool DrawRuleRow(Configuration cfg, ThenRule rule)
    {
        ImGui.PushID(rule.Id);
        Bullet(rule);
        ImGui.SameLine();
        if (ImGui.Selectable(rule.Name, selectedRuleId == rule.Id))
        {
            selectedRuleId = rule.Id;
            selectedFolder = rule.FolderKey;
        }
        if (ImGui.BeginDragDropSource())
        {
            dragRuleId = rule.Id;
            ImGui.SetDragDropPayload("ANDTHEN_RULE", Encoding.UTF8.GetBytes(rule.Id));
            ImGui.TextUnformatted(rule.Name);
            ImGui.EndDragDropSource();
        }
        var kill = false;
        if (ImGui.BeginPopupContextItem("rule"))
        {
            if (ImGui.MenuItem("Duplicate")) plugin.DuplicateRule(rule);
            if (ImGui.MenuItem("Copy JSON")) { ImGui.SetClipboardText(Share.ToJson(rule)); importMsg = "Copied JSON."; }
            if (ImGui.MenuItem("Copy AT1")) { ImGui.SetClipboardText(Share.Encode(rule)); importMsg = "Copied share code."; }
            if (ImGui.MenuItem("Run THEN")) plugin.TestRule(rule);
            if (ImGui.GetIO().KeyShift) { if (ImGui.MenuItem("Delete")) kill = true; }
            else ImGui.MenuItem("Delete (hold Shift)", false);
            ImGui.EndPopup();
        }
        ImGui.PopID();
        return kill;
    }

    private void DrawBottomBar(Configuration cfg)
    {
        ImGui.Separator();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##nn", "Name for new rule or folder", ref newName, 64);
        if (IconButton(FontAwesomeIcon.Plus, "nr", "New rule in the selected folder")) AddRule(cfg, selectedFolder);
        ImGui.SameLine();
        if (IconButton(FontAwesomeIcon.FolderPlus, "nf", "New folder")) AddFolder(cfg);
        ImGui.SameLine();
        if (IconButton(FontAwesomeIcon.Paste, "np", "Paste rule(s) from clipboard"))
            importMsg = plugin.TryImport(ImGui.GetClipboardText() ?? string.Empty, out var err) ? "Imported." : err;
        ImGui.SameLine();
        var canDel = selectedRuleId is not null && ImGui.GetIO().KeyShift;
        if (!canDel) ImGui.BeginDisabled();
        if (IconButton(FontAwesomeIcon.Trash, "nd", "Delete selected rule (hold Shift)") && selectedRuleId is not null)
        {
            cfg.Rules.RemoveAll(r => r.Id == selectedRuleId);
            selectedRuleId = null;
            cfg.Save();
        }
        if (!canDel) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Select a rule, hold Shift, then delete.");
    }

    private void AddRule(Configuration cfg, string folder)
    {
        var rule = new ThenRule
        {
            Name = string.IsNullOrWhiteSpace(newName) ? "New rule" : newName.Trim(),
            Enabled = true,
            Mode = ApplyMode.Off,
            Folder = folder,
            DelaySec = cfg.DefaultDelaySec,
        };
        cfg.Rules.Add(rule);
        OpenRule(rule.Id);
        newName = string.Empty;
        cfg.Save();
    }

    private void AddFolder(Configuration cfg)
    {
        var name = string.IsNullOrWhiteSpace(newName) ? "New folder" : newName.Trim().Replace('\\', '/');
        if (selectedFolder.Length > 0 && !name.Contains('/'))
            name = selectedFolder + "/" + name;
        if (!cfg.Folders.Contains(name, StringComparer.OrdinalIgnoreCase))
            cfg.Folders.Add(name);
        selectedFolder = name;
        openFolders.Add(name);
        newName = string.Empty;
        cfg.Save();
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

    private void Bullet(ThenRule rule)
    {
        Vector4 color = !rule.Enabled ? new Vector4(0.35f, 0.35f, 0.35f, 1)
            : plugin.Engine.JustApplied(rule.Id) ? new Vector4(0.92f, 0.62f, 0.16f, 1)
            : plugin.Engine.LastMatches.Any(r => r.Id == rule.Id) ? new Vector4(0.17f, 0.70f, 0.69f, 1)
            : new Vector4(0.55f, 0.55f, 0.55f, 1);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted("\u2022");
        ImGui.PopStyleColor();
    }

    private static bool IconButton(FontAwesomeIcon icon, string id, string tip)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var clicked = ImGui.Button($"{icon.ToIconString()}##{id}");
        ImGui.PopFont();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
        return clicked;
    }

    private bool Visible(ThenRule rule)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        if (rule.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
        if ((rule.Notes ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
        if ((rule.Folder ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string FolderParent(string folder)
    {
        var i = folder.LastIndexOf('/');
        return i < 0 ? string.Empty : folder[..i];
    }

    private static string FolderLeaf(string folder)
    {
        var i = folder.LastIndexOf('/');
        return i < 0 ? folder : folder[(i + 1)..];
    }
}
