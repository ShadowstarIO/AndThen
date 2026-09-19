using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AndThen.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private static readonly string[] States =
    [
        "InDuty", "InCombat", "Cutscene", "Mounted", "Flying", "Swimming", "Diving",
        "Crafting", "Gathering", "Dead", "Occupied", "BetweenAreas", "Jumping",
        "Casting", "Fishing", "PvP", "UsingHousing", "WeaponDrawn", "InParty",
    ];
    private static readonly string[] Jobs =
    [
        "PLD", "WAR", "DRK", "GNB", "WHM", "SCH", "AST", "SGE",
        "MNK", "DRG", "NIN", "SAM", "RPR", "VPR",
        "BRD", "MCH", "DNC", "BLM", "SMN", "RDM", "PCT", "BLU",
        "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL",
        "MIN", "BTN", "FSH",
    ];
    private static readonly string[] Roles = ["Tank", "Healer", "DPS", "Crafter", "Gatherer"];
    private static readonly string[] Duties = ["any", "none", "dungeon", "raid", "alliance"];
    private static readonly string[] Settings = ["Fps", "MouseLock", "MusicOn", "SoundOn", "DisplayHead", "DisplayWeapon", "HudLayout"];
    private static readonly string[] Statuses = ["Online", "Away", "Busy", "Roleplaying", "LookingToMeld", "LookingForParty"];

    private readonly Plugin plugin;
    private string newRuleName = string.Empty;
    private string newFolder = string.Empty;
    private string filter = string.Empty;
    private string selectedFolder = "All";
    private string? selectedRuleId;
    private string importMsg = string.Empty;
    private string chipValue = string.Empty;
    private int chipKind;
    private int thenKind;

    public MainWindow(Plugin plugin) : base($"AndThen v{Plugin.AppVersion}###AndThenMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(860, 520),
            MaximumSize = new Vector2(1600, 1400),
        };
    }

    public void Dispose() { }

    public void OpenRule(string id)
    {
        selectedRuleId = id;
        IsOpen = true;
    }

    public override void Draw()
    {
        WindowName = $"AndThen v{Plugin.AppVersion}###AndThenMain";
        var cfg = plugin.Configuration;

        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) { cfg.Enabled = enabled; cfg.Save(); }
        ImGui.SameLine();
        if (ImGui.Button("Apply now")) plugin.ApplyNow();
        ImGui.SameLine();
        if (ImGui.Button("Settings")) plugin.ToggleConfigUi();
        ImGui.SameLine();
        ImGui.TextDisabled(plugin.IsPaused ? "Paused" : plugin.Snapshot().Line());

        var matches = plugin.CurrentMatches();
        ImGui.TextUnformatted(matches.Count == 0 ? "Current match: none" : "Current match:");
        foreach (var rule in matches.Take(6))
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"P{rule.Priority} {rule.Name}"))
                OpenRule(rule.Id);
        }
        ImGui.Separator();

        ImGui.SetNextItemWidth(180);
        ImGui.InputTextWithHint("##new", "New rule name", ref newRuleName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add rule"))
        {
            var prio = cfg.Rules.Count == 0 ? 10 : cfg.Rules.Max(r => r.Priority) + 10;
            var rule = new ThenRule
            {
                Name = string.IsNullOrWhiteSpace(newRuleName) ? "New rule" : newRuleName.Trim(),
                Priority = prio,
                Enabled = false,
                Folder = selectedFolder is "All" or "Ungrouped" ? string.Empty : selectedFolder,
            };
            cfg.Rules.Add(rule);
            OpenRule(rule.Id);
            newRuleName = string.Empty;
            cfg.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Import"))
            importMsg = plugin.TryImport(ImGui.GetClipboardText() ?? string.Empty, out var err) ? "Imported." : err;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        ImGui.InputTextWithHint("##filter", "Filter", ref filter, 64);
        if (!string.IsNullOrEmpty(importMsg))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(importMsg);
        }

        var avail = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("left", new Vector2(Math.Min(320, avail.X * 0.38f), 0), true);
        DrawList(cfg);
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("right", new Vector2(0, 0), true);
        var selected = cfg.Rules.Find(r => r.Id == selectedRuleId);
        if (selected is null) ImGui.TextDisabled("Select a rule.");
        else DrawEditor(cfg, selected);
        ImGui.EndChild();
    }

    private void DrawList(Configuration cfg)
    {
        var folders = cfg.Rules.Select(r => r.FolderKey).Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f).ToList();
        if (ImGui.Selectable("All", selectedFolder == "All")) selectedFolder = "All";
        if (ImGui.Selectable("Ungrouped", selectedFolder == "Ungrouped")) selectedFolder = "Ungrouped";
        if (folders.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled("Folders");
        }
        foreach (var folder in folders)
        {
            if (ImGui.Selectable(folder, selectedFolder.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                selectedFolder = folder;
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##nf", "New folder", ref newFolder, 40);
        ImGui.Separator();

        ThenRule? remove = null;
        foreach (var rule in cfg.Rules.Where(Visible).OrderBy(r => r.Priority))
        {
            ImGui.PushID(rule.Id);
            var on = rule.Enabled;
            if (ImGui.Checkbox("##on", ref on)) { rule.Enabled = on; cfg.Save(); }
            ImGui.SameLine();
            var hit = selectedRuleId == rule.Id;
            if (ImGui.Selectable($"P{rule.Priority}  {rule.Name}", hit))
                selectedRuleId = rule.Id;
            if (ImGui.BeginPopupContextItem("ctx"))
            {
                if (ImGui.MenuItem("Move down")) plugin.MovePriority(rule, 1);
                if (ImGui.MenuItem("Move up")) plugin.MovePriority(rule, -1);
                if (ImGui.MenuItem("Duplicate")) plugin.DuplicateRule(rule);
                if (ImGui.MenuItem("Copy JSON")) { ImGui.SetClipboardText(Share.ToJson(rule)); importMsg = "Copied JSON."; }
                if (ImGui.MenuItem("Copy AT1")) { ImGui.SetClipboardText(Share.Encode(rule)); importMsg = "Copied share code."; }
                if (ImGui.MenuItem("Run THEN")) plugin.TestRule(rule);
                if (ImGui.GetIO().KeyShift)
                {
                    if (ImGui.MenuItem("Delete")) remove = rule;
                }
                else ImGui.MenuItem("Delete (hold Shift)", false);
                ImGui.EndPopup();
            }
            ImGui.PopID();
        }
        if (remove is not null)
        {
            cfg.Rules.Remove(remove);
            if (selectedRuleId == remove.Id) selectedRuleId = null;
            cfg.Save();
        }
    }

    private bool Visible(ThenRule rule)
    {
        if (!string.IsNullOrWhiteSpace(filter)
            && !rule.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            && !(rule.Notes ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase)
            && !(rule.Folder ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase))
            return false;
        if (selectedFolder == "All") return true;
        if (selectedFolder == "Ungrouped") return string.IsNullOrWhiteSpace(rule.Folder);
        return rule.FolderKey.Equals(selectedFolder, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawEditor(Configuration cfg, ThenRule rule)
    {
        ImGui.TextUnformatted($"EDITING P{rule.Priority}: {rule.Name}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Test")) plugin.TestRule(rule);
        ImGui.SameLine();
        if (ImGui.SmallButton("Duplicate")) plugin.DuplicateRule(rule);

        var name = rule.Name;
        if (ImGui.InputText("Name", ref name, 64) && name != rule.Name) { rule.Name = name; cfg.Save(); }
        var folder = rule.Folder;
        if (ImGui.InputText("Folder", ref folder, 40) && folder != rule.Folder) { rule.Folder = folder; cfg.Save(); }
        var notes = rule.Notes;
        if (ImGui.InputText("Notes", ref notes, 120) && notes != rule.Notes) { rule.Notes = notes; cfg.Save(); }

        ImGui.Separator();
        ImGui.TextUnformatted("IF  (AND all of these)");
        DrawChips(cfg, rule.AndChips, "and");
        ImGui.TextUnformatted("OR  (any of these, if any are set)");
        DrawChips(cfg, rule.OrChips, "or");
        ImGui.TextUnformatted("NOT");
        DrawChips(cfg, rule.NotChips, "not");

        ImGui.Separator();
        ImGui.TextUnformatted("THEN  (runs once when the rule becomes true)");
        ThenRow? drop = null;
        for (var i = 0; i < rule.Then.Count; i++)
        {
            var row = rule.Then[i];
            ImGui.PushID(i);
            ImGui.TextDisabled($"{i + 1}.");
            ImGui.SameLine();
            ImGui.TextUnformatted(row.Label);
            ImGui.SameLine();
            if (ImGui.SmallButton("X")) drop = row;
            ImGui.PopID();
        }
        if (drop is not null) { rule.Then.Remove(drop); cfg.Save(); }

        ImGui.SetNextItemWidth(140);
        ImGui.Combo("##thenkind", ref thenKind, "Command\0Wait\0Setting\0Status\0");
        ImGui.SameLine();
        if (ImGui.Button("Add THEN"))
        {
            rule.Then.Add(NewThen((ThenKind)thenKind));
            cfg.Save();
        }

        if (rule.Then.Count > 0)
            DrawThenEdit(cfg, rule.Then[^1]);
    }

    private void DrawChips(Configuration cfg, System.Collections.Generic.List<RuleChip> chips, string id)
    {
        ImGui.PushID(id);
        RuleChip? drop = null;
        foreach (var chip in chips)
        {
            ImGui.PushID(chip.Kind + chip.Value);
            if (ImGui.SmallButton(chip.Label + " x")) drop = chip;
            ImGui.SameLine();
            ImGui.PopID();
        }
        if (drop is not null) { chips.Remove(drop); cfg.Save(); }
        ImGui.NewLine();
        ImGui.SetNextItemWidth(110);
        ImGui.Combo("##kind", ref chipKind, "State\0Job\0Role\0Zone\0World\0DataCenter\0PartySize\0Duty\0");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140);
        ImGui.InputTextWithHint("##val", HintFor((ChipKind)chipKind), ref chipValue, 48);
        ImGui.SameLine();
        if (ImGui.Button("Add chip") && !string.IsNullOrWhiteSpace(chipValue))
        {
            chips.Add(new RuleChip { Kind = (ChipKind)chipKind, Value = chipValue.Trim() });
            chipValue = string.Empty;
            cfg.Save();
        }
        DrawQuick((ChipKind)chipKind, chips, cfg);
        ImGui.PopID();
    }

    private void DrawQuick(ChipKind kind, System.Collections.Generic.List<RuleChip> chips, Configuration cfg)
    {
        string[]? opts = kind switch
        {
            ChipKind.State => States,
            ChipKind.Job => Jobs,
            ChipKind.Role => Roles,
            ChipKind.Duty => Duties,
            _ => null,
        };
        if (opts is null) return;
        var i = 0;
        foreach (var opt in opts)
        {
            if (i++ > 0 && i % 8 != 1) ImGui.SameLine();
            if (ImGui.SmallButton(opt))
            {
                chips.Add(new RuleChip { Kind = kind, Value = opt });
                cfg.Save();
            }
        }
    }

    private static ThenRow NewThen(ThenKind kind) => kind switch
    {
        ThenKind.Wait => new ThenRow { Kind = ThenKind.Wait, WaitMs = 250, Value = "250" },
        ThenKind.Setting => new ThenRow { Kind = ThenKind.Setting, Setting = SettingKey.Fps, Value = "30" },
        ThenKind.Status => new ThenRow { Kind = ThenKind.Status, Value = "Busy" },
        _ => new ThenRow { Kind = ThenKind.Command, Value = "/busy on" },
    };

    private static void DrawThenEdit(Configuration cfg, ThenRow row)
    {
        ImGui.Indent();
        switch (row.Kind)
        {
            case ThenKind.Command:
            {
                var cmd = row.Value;
                if (ImGui.InputText("Command", ref cmd, 180)) { row.Value = cmd; cfg.Save(); }
                break;
            }
            case ThenKind.Wait:
            {
                var ms = row.WaitMs;
                if (ImGui.InputInt("Wait ms", ref ms))
                {
                    row.WaitMs = Math.Max(0, ms);
                    row.Value = row.WaitMs.ToString();
                    cfg.Save();
                }
                break;
            }
            case ThenKind.Setting:
            {
                var si = (int)row.Setting;
                if (ImGui.Combo("Setting", ref si, string.Join('\0', Settings) + "\0"))
                {
                    row.Setting = (SettingKey)si;
                    cfg.Save();
                }
                var val = row.Value;
                if (ImGui.InputText("Value", ref val, 32)) { row.Value = val; cfg.Save(); }
                ImGui.TextDisabled("Fps: none / 60 / 30   MouseLock: on / off   HudLayout: 1-4");
                break;
            }
            case ThenKind.Status:
            {
                var val = row.Value;
                if (ImGui.InputText("Status", ref val, 32)) { row.Value = val; cfg.Save(); }
                foreach (var s in Statuses)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton(s)) { row.Value = s; cfg.Save(); }
                }
                break;
            }
        }
        ImGui.Unindent();
    }

    private static string HintFor(ChipKind kind) => kind switch
    {
        ChipKind.PartySize => ">=6",
        ChipKind.Zone => "zone name or id",
        ChipKind.World => "world name",
        ChipKind.DataCenter => "data center",
        ChipKind.Job => "MNK",
        _ => "value",
    };
}
