using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AndThen.Windows;

public sealed class MainWindow : Window, IDisposable
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
    private readonly HashSet<string> openFolders = new(StringComparer.OrdinalIgnoreCase) { "Examples" };

    public MainWindow(Plugin plugin) : base($"AndThen v{Plugin.AppVersion}###AndThenMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(920, 560),
            MaximumSize = new Vector2(1800, 1400),
        };
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
        foreach (var rule in matches.Take(8))
        {
            ImGui.SameLine();
            if (ImGui.SmallButton(rule.Name)) OpenRule(rule.Id);
        }

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

        var folders = cfg.Folders
            .Concat(cfg.Rules.Select(r => r.FolderKey))
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var folder in folders)
            DrawFolder(cfg, folder);

        ImGui.Separator();
        ThenRule? remove = null;
        if (selectedFolder is "All" or "Ungrouped")
        {
            foreach (var rule in cfg.Rules.Where(r => Visible(r) && (selectedFolder == "All" || string.IsNullOrWhiteSpace(r.Folder))))
            {
                if (DrawRuleRow(cfg, rule)) remove = rule;
            }
        }

        ImGui.Separator();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##nf", "New folder", ref newFolder, 40);
        if (ImGui.Button("Add folder") && !string.IsNullOrWhiteSpace(newFolder))
        {
            if (!cfg.Folders.Contains(newFolder.Trim(), StringComparer.OrdinalIgnoreCase))
                cfg.Folders.Add(newFolder.Trim());
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
                Enabled = true,
                Mode = ApplyMode.Off,
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
        var muted = cfg.MutedFolders.Contains(folder);
        if (ImGui.SmallButton(open ? "-" : "+"))
        {
            if (open) openFolders.Remove(folder);
            else openFolders.Add(folder);
        }
        ImGui.SameLine();
        var mute = muted;
        if (ImGui.Checkbox($"##m{folder}", ref mute))
        {
            if (mute && !cfg.MutedFolders.Contains(folder)) cfg.MutedFolders.Add(folder);
            if (!mute) cfg.MutedFolders.RemoveAll(f => f.Equals(folder, StringComparison.OrdinalIgnoreCase));
            cfg.Save();
        }
        ImGui.SameLine();
        if (ImGui.Selectable(folder, selectedFolder.Equals(folder, StringComparison.OrdinalIgnoreCase)))
            selectedFolder = folder;
        DropFolder(cfg, folder);

        if (!openFolders.Contains(folder)) return;
        ImGui.Indent();
        ThenRule? remove = null;
        foreach (var rule in cfg.Rules.Where(r => r.FolderKey.Equals(folder, StringComparison.OrdinalIgnoreCase) && Visible(r)))
        {
            if (DrawRuleRow(cfg, rule)) remove = rule;
        }
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
        var hit = selectedRuleId == rule.Id;
        var label = string.IsNullOrWhiteSpace(rule.Notes) ? rule.Name : $"{rule.Name}  ";
        if (ImGui.Selectable(label, hit))
            selectedRuleId = rule.Id;
        if (!string.IsNullOrWhiteSpace(rule.Notes))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(TrimOne(rule.Notes, 28));
        }
        if (ImGui.BeginDragDropSource())
        {
            ImGui.SetDragDropPayload("ANDTHEN_RULE", rule.Id);
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
        if (payload.HasValue && payload.Value is string id)
        {
            var rule = cfg.Rules.Find(r => r.Id == id);
            if (rule is not null)
            {
                rule.Folder = folder;
                cfg.Save();
            }
        }
        ImGui.EndDragDropTarget();
    }

    private void Led(ThenRule rule)
    {
        Vector4 color;
        if (!rule.Enabled) color = new Vector4(0.35f, 0.35f, 0.35f, 1);
        else if (plugin.Engine.JustApplied(rule.Id)) color = new Vector4(0.92f, 0.62f, 0.16f, 1);
        else if (plugin.Engine.LastMatches.Any(r => r.Id == rule.Id)) color = new Vector4(0.17f, 0.70f, 0.69f, 1);
        else color = new Vector4(0.55f, 0.55f, 0.55f, 1);
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

    private void DrawEditor(Configuration cfg, ThenRule rule)
    {
        var name = rule.Name;
        ImGui.SetNextItemWidth(240);
        if (ImGui.InputText("Name", ref name, 64) && name != rule.Name) { rule.Name = name; cfg.Save(); }
        ImGui.SameLine();
        var notes = rule.Notes;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##note", "Note", ref notes, 160) && notes != rule.Notes) { rule.Notes = notes; cfg.Save(); }

        var mode = (int)rule.Mode;
        if (ImGui.Combo("Mode", ref mode, "Off\0Dialog\0Auto\0"))
        {
            rule.Mode = (ApplyMode)mode;
            cfg.Save();
        }
        ImGui.SameLine();
        var on = rule.Enabled;
        if (ImGui.Checkbox("On", ref on)) { rule.Enabled = on; cfg.Save(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Test")) plugin.TestRule(rule);
        ImGui.SameLine();
        if (ImGui.SmallButton("Duplicate")) plugin.DuplicateRule(rule);
        ImGui.TextDisabled($"/atn {rule.CommandToken}");

        ImGui.Separator();
        DrawChipBuilder(cfg, rule);
        ImGui.TextUnformatted("IF  (all of these)");
        DrawChips(cfg, rule.AndChips, "and");
        ImGui.TextUnformatted("OR  (any, if any are set)");
        DrawChips(cfg, rule.OrChips, "or");
        ImGui.TextUnformatted("NOT");
        DrawChips(cfg, rule.NotChips, "not");

        ImGui.Separator();
        DrawThenBuilder(cfg, rule);
        ImGui.TextUnformatted("THEN");
        DrawThenList(cfg, rule);
    }

    private void DrawChipBuilder(Configuration cfg, ThenRule rule)
    {
        ImGui.SetNextItemWidth(110);
        ImGui.Combo("##ck", ref chipKind, string.Join('\0', Catalog.ChipKinds) + "\0");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        ImGui.InputTextWithHint("##cv", HintFor((ChipKind)chipKind), ref chipValue, 48);
        ImGui.SameLine();
        if (ImGui.Button("+IF")) AddChip(cfg, rule.AndChips);
        ImGui.SameLine();
        if (ImGui.Button("+OR")) AddChip(cfg, rule.OrChips);
        ImGui.SameLine();
        if (ImGui.Button("+NOT")) AddChip(cfg, rule.NotChips);
        DrawQuick((ChipKind)chipKind, cfg, rule);
    }

    private void AddChip(Configuration cfg, List<RuleChip> list)
    {
        if (string.IsNullOrWhiteSpace(chipValue)) return;
        list.Add(new RuleChip { Kind = (ChipKind)chipKind, Value = chipValue.Trim() });
        chipValue = string.Empty;
        cfg.Save();
    }

    private void DrawQuick(ChipKind kind, Configuration cfg, ThenRule rule)
    {
        string[]? opts = kind switch
        {
            ChipKind.State => Catalog.States,
            ChipKind.Job => Catalog.Jobs,
            ChipKind.Role => Catalog.Roles,
            ChipKind.Duty => Catalog.Duties,
            ChipKind.Group => Catalog.Groups,
            ChipKind.Nearby => Catalog.Nearby,
            _ => null,
        };
        if (opts is null) return;
        var wrap = ImGui.GetContentRegionAvail().X;
        var x0 = ImGui.GetCursorPosX();
        foreach (var opt in opts)
        {
            var w = ImGui.CalcTextSize(opt).X + 16;
            if (ImGui.GetCursorPosX() - x0 + w > wrap) ImGui.NewLine();
            if (ImGui.SmallButton(opt))
            {
                rule.AndChips.Add(new RuleChip { Kind = kind, Value = opt });
                cfg.Save();
            }
            ImGui.SameLine();
        }
        ImGui.NewLine();
    }

    private void DrawChips(Configuration cfg, List<RuleChip> chips, string id)
    {
        ImGui.PushID(id);
        RuleChip? drop = null;
        var wrap = ImGui.GetContentRegionAvail().X;
        var x0 = ImGui.GetCursorPosX();
        foreach (var chip in chips)
        {
            var label = chip.Label + " x";
            var w = ImGui.CalcTextSize(label).X + 16;
            if (ImGui.GetCursorPosX() - x0 + w > wrap) ImGui.NewLine();
            ImGui.PushID(chip.Kind + chip.Value);
            if (ImGui.SmallButton(label)) drop = chip;
            ImGui.SameLine();
            ImGui.PopID();
        }
        ImGui.NewLine();
        if (drop is not null) { chips.Remove(drop); cfg.Save(); }
        ImGui.PopID();
    }

    private void DrawThenBuilder(Configuration cfg, ThenRule rule)
    {
        ImGui.SetNextItemWidth(110);
        ImGui.Combo("##tk", ref thenKind, string.Join('\0', Catalog.ThenKinds) + "\0");
        var kind = (ThenKind)thenKind;
        if (kind == ThenKind.Command)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##tv", "/command", ref thenValue, 180);
        }
        else if (kind == ThenKind.Wait)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("ms", ref waitMs);
        }
        else if (kind == ThenKind.Status)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(160);
            ImGui.InputTextWithHint("##st", "Busy", ref thenValue, 32);
            foreach (var s in Catalog.Statuses)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton(s)) thenValue = s;
            }
        }
        else
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.Combo("##sec", ref configSection, "System\0Ui\0");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(160);
            ImGui.InputTextWithHint("##oq", "Search option", ref optionQuery, 48);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            ImGui.InputTextWithHint("##ov", "value", ref thenValue, 24);
            var names = configSection == 0 ? Catalog.SystemOptions : Catalog.UiOptions;
            var hits = Catalog.Filter(names, optionQuery).Take(10);
            foreach (var opt in hits)
            {
                if (ImGui.SmallButton(opt)) pickedOption = opt;
                ImGui.SameLine();
            }
            ImGui.NewLine();
            if (!string.IsNullOrWhiteSpace(pickedOption))
                ImGui.TextDisabled($"Picked {pickedOption}");
        }

        if (ImGui.Button("+ THEN"))
        {
            rule.Then.Add(kind switch
            {
                ThenKind.Wait => new ThenRow { Kind = ThenKind.Wait, WaitMs = Math.Max(0, waitMs), Value = waitMs.ToString() },
                ThenKind.Status => new ThenRow { Kind = ThenKind.Status, Value = string.IsNullOrWhiteSpace(thenValue) ? "Busy" : thenValue.Trim() },
                ThenKind.Config => new ThenRow
                {
                    Kind = ThenKind.Config,
                    Section = configSection == 0 ? ConfigSection.System : ConfigSection.Ui,
                    Option = string.IsNullOrWhiteSpace(pickedOption) ? optionQuery.Trim() : pickedOption,
                    Value = thenValue.Trim(),
                },
                _ => new ThenRow { Kind = ThenKind.Command, Value = string.IsNullOrWhiteSpace(thenValue) ? "/busy on" : thenValue.Trim() },
            });
            cfg.Save();
        }
    }

    private static void DrawThenList(Configuration cfg, ThenRule rule)
    {
        ThenRow? drop = null;
        var wrap = ImGui.GetContentRegionAvail().X;
        var x0 = ImGui.GetCursorPosX();
        for (var i = 0; i < rule.Then.Count; i++)
        {
            var row = rule.Then[i];
            var label = $"{i + 1}. {row.Label} x";
            var w = ImGui.CalcTextSize(label).X + 16;
            if (ImGui.GetCursorPosX() - x0 + w > wrap) ImGui.NewLine();
            ImGui.PushID(i);
            if (ImGui.SmallButton(label)) drop = row;
            ImGui.SameLine();
            ImGui.PopID();
        }
        ImGui.NewLine();
        if (drop is not null) { rule.Then.Remove(drop); cfg.Save(); }
    }

    private static string HintFor(ChipKind kind) => kind switch
    {
        ChipKind.PartySize => ">=6",
        ChipKind.Zone => "zone name or id",
        ChipKind.World => "world name",
        ChipKind.DataCenter => "data center",
        ChipKind.Job => "MNK",
        ChipKind.Time => "et>=18  or  Saturday",
        ChipKind.Weather => "Rain",
        ChipKind.Nearby => "empty / few / crowded",
        ChipKind.Group => "Light",
        _ => "value",
    };

    private static string TrimOne(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
