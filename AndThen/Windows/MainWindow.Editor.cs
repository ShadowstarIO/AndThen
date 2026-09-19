using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AndThen.Windows;

public sealed partial class MainWindow
{
    private void DrawEditor(Configuration cfg, ThenRule rule)
    {
        var name = rule.Name;
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputText("Name", ref name, 64) && name != rule.Name) { rule.Name = name; cfg.Save(); }
        ImGui.SameLine();
        var notes = rule.Notes;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##note", "Note — also Dialog text", ref notes, 160) && notes != rule.Notes) { rule.Notes = notes; cfg.Save(); }
        var mode = (int)rule.Mode;
        if (ImGui.Combo("Mode", ref mode, "Off\0Dialog\0Auto\0")) { rule.Mode = (ApplyMode)mode; cfg.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Off: /atn Name or Test only.\nDialog: ask once when it becomes true.\nAuto: run once on rising edge.");
        ImGui.SameLine();
        var on = rule.Enabled;
        if (ImGui.Checkbox("On", ref on)) { rule.Enabled = on; cfg.Save(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Test")) plugin.TestRule(rule);
        ImGui.SameLine();
        if (ImGui.SmallButton("Why")) foreach (var line in plugin.Engine.Why(rule, plugin.Snapshot())) Plugin.Notify(line);
        ImGui.SameLine();
        if (ImGui.SmallButton("Duplicate")) plugin.DuplicateRule(rule);
        ImGui.SameLine();
        if (ImGui.SmallButton("JSON")) { ImGui.SetClipboardText(Share.ToJson(rule)); importMsg = "Copied JSON."; }
        ImGui.SameLine();
        if (ImGui.SmallButton("AT1")) { ImGui.SetClipboardText(Share.Encode(rule)); importMsg = "Copied share code."; }
        ImGui.TextDisabled($"/atn {rule.CommandToken}   ·   teal chip = true now");
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
            ChipKind.Place => Catalog.Places,
            ChipKind.Target => Catalog.Targets,
            ChipKind.Time => Catalog.Times,
            _ => null,
        };
        if (opts is null) return;
        var wrap = ImGui.GetContentRegionAvail().X;
        var x0 = ImGui.GetCursorPosX();
        foreach (var opt in opts)
        {
            var w = ImGui.CalcTextSize(opt).X + 16;
            if (ImGui.GetCursorPosX() - x0 + w > wrap) ImGui.NewLine();
            if (ImGui.SmallButton(opt)) { rule.AndChips.Add(new RuleChip { Kind = kind, Value = opt }); cfg.Save(); }
            ImGui.SameLine();
        }
        ImGui.NewLine();
    }

    private void DrawChips(Configuration cfg, List<RuleChip> chips, string id)
    {
        ImGui.PushID(id);
        RuleChip? drop = null;
        var snap = plugin.Snapshot();
        var wrap = ImGui.GetContentRegionAvail().X;
        var x0 = ImGui.GetCursorPosX();
        foreach (var chip in chips)
        {
            var label = chip.Label + " x";
            var w = ImGui.CalcTextSize(label).X + 16;
            if (ImGui.GetCursorPosX() - x0 + w > wrap) ImGui.NewLine();
            ImGui.PushID(chip.Kind + chip.Value);
            var live = ChipEval.ChipTrue(chip, snap);
            if (live) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.12f, 0.42f, 0.42f, 1f));
            if (ImGui.SmallButton(label)) drop = chip;
            if (live) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(live ? "true now" : "false now");
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
        if (kind == ThenKind.Command) { ImGui.SameLine(); ImGui.SetNextItemWidth(220); ImGui.InputTextWithHint("##tv", "/command", ref thenValue, 180); }
        else if (kind == ThenKind.Wait) { ImGui.SameLine(); ImGui.SetNextItemWidth(100); ImGui.InputInt("ms", ref waitMs); }
        else if (kind == ThenKind.Status)
        {
            ImGui.SameLine(); ImGui.SetNextItemWidth(160); ImGui.InputTextWithHint("##st", "Busy", ref thenValue, 32);
            foreach (var s in Catalog.Statuses) { ImGui.SameLine(); if (ImGui.SmallButton(s)) thenValue = s; }
        }
        else if (kind == ThenKind.Notify) { ImGui.SameLine(); ImGui.SetNextItemWidth(280); ImGui.InputTextWithHint("##nv", "chat line", ref thenValue, 120); }
        else
        {
            ImGui.SameLine(); ImGui.SetNextItemWidth(90); ImGui.Combo("##sec", ref configSection, "System\0Ui\0");
            ImGui.SameLine(); ImGui.SetNextItemWidth(160); ImGui.InputTextWithHint("##oq", "Search option", ref optionQuery, 48);
            ImGui.SameLine(); ImGui.SetNextItemWidth(80); ImGui.InputTextWithHint("##ov", "value", ref thenValue, 24);
            string? lastGroup = null;
            foreach (var opt in Catalog.Filter(configSection == 0 ? Catalog.SystemOptions : Catalog.UiOptions, optionQuery).Take(12))
            {
                var group = Catalog.GroupOf(opt);
                if (group != lastGroup) { if (lastGroup is not null) ImGui.NewLine(); ImGui.TextDisabled(group); lastGroup = group; }
                if (ImGui.SmallButton(opt)) pickedOption = opt;
                ImGui.SameLine();
            }
            ImGui.NewLine();
            if (!string.IsNullOrWhiteSpace(pickedOption)) ImGui.TextDisabled($"Picked {pickedOption}");
        }
        if (ImGui.Button("+ THEN"))
        {
            rule.Then.Add(kind switch
            {
                ThenKind.Wait => new ThenRow { Kind = ThenKind.Wait, WaitMs = Math.Max(0, waitMs), Value = waitMs.ToString() },
                ThenKind.Status => new ThenRow { Kind = ThenKind.Status, Value = string.IsNullOrWhiteSpace(thenValue) ? "Busy" : thenValue.Trim() },
                ThenKind.Notify => new ThenRow { Kind = ThenKind.Notify, Value = string.IsNullOrWhiteSpace(thenValue) ? rule.Notes : thenValue.Trim() },
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
        ChipKind.Time => "night  or  et>=18",
        ChipKind.Weather => "Rain",
        ChipKind.Nearby => "empty / few / crowded",
        ChipKind.Group => "Light",
        ChipKind.Place => "Town / Housing / Indoor",
        ChipKind.Target => "none / player / npc",
        _ => "value",
    };

    private static string TrimOne(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
