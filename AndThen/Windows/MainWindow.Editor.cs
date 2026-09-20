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
        if (ImGui.InputTextWithHint("##note", "Note — also the Dialog text", ref notes, 160) && notes != rule.Notes) { rule.Notes = notes; cfg.Save(); }

        var mode = (int)rule.Mode;
        if (ImGui.Combo("When this matches", ref mode, "Off — only /atn Name or Test\0Dialog — ask once\0Auto — run once on rising edge\0"))
        {
            rule.Mode = (ApplyMode)mode;
            cfg.Save();
        }
        ImGui.SameLine();
        var on = rule.Enabled;
        if (ImGui.Checkbox("Enabled", ref on)) { rule.Enabled = on; cfg.Save(); }

        if (ImGui.Button("Test THEN")) plugin.TestRule(rule);
        ImGui.SameLine();
        if (ImGui.Button("Why")) foreach (var line in plugin.Engine.Why(rule, plugin.Snapshot())) Plugin.Notify(line);
        ImGui.SameLine();
        if (ImGui.Button("Duplicate")) plugin.DuplicateRule(rule);
        ImGui.SameLine();
        if (ImGui.Button("Copy JSON")) { ImGui.SetClipboardText(Share.ToJson(rule)); importMsg = "Copied JSON."; }
        ImGui.SameLine();
        if (ImGui.Button("Copy AT1")) { ImGui.SetClipboardText(Share.Encode(rule)); importMsg = "Copied share code."; }
        ImGui.SameLine();
        if (ImGui.Button("Paste"))
            importMsg = plugin.TryImport(ImGui.GetClipboardText() ?? string.Empty, out var err) ? "Imported." : err;
        ImGui.TextDisabled($"/atn {rule.CommandToken} always runs this rule. Teal chip = true right now.");

        ImGui.Separator();
        ImGui.TextUnformatted("1. Pick a condition");
        DrawChipPicker();
        ImGui.TextDisabled("Draft: " + DraftLabel());
        if (ImGui.Button("Add to IF")) AddChip(cfg, rule.AndChips);
        ImGui.SameLine();
        if (ImGui.Button("Add to OR")) AddChip(cfg, rule.OrChips);
        ImGui.SameLine();
        if (ImGui.Button("Add to NOT")) AddChip(cfg, rule.NotChips);

        ImGui.Separator();
        ImGui.TextUnformatted("IF — all of these");
        DrawChips(cfg, rule.AndChips, "and");
        ImGui.TextUnformatted("OR — any one of these, if any are set");
        DrawChips(cfg, rule.OrChips, "or");
        ImGui.TextUnformatted("NOT — none of these");
        DrawChips(cfg, rule.NotChips, "not");

        ImGui.Separator();
        ImGui.TextUnformatted("2. Pick a THEN action");
        DrawThenPicker();
        if (ImGui.Button("Add THEN")) AddThen(cfg, rule);
        ImGui.TextUnformatted("THEN — runs in this order");
        DrawThenList(cfg, rule);
    }

    private void DrawChipPicker()
    {
        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Kind", ref chipKind, string.Join('\0', Catalog.ChipKinds) + "\0");
        var kind = (ChipKind)chipKind;
        var opts = Catalog.OptionsFor(kind);

        if (kind == ChipKind.PartySize)
        {
            ImGui.SetNextItemWidth(80);
            var cmp = Array.IndexOf(Catalog.Compare, partyCmp);
            if (cmp < 0) cmp = 0;
            if (ImGui.Combo("Compare", ref cmp, string.Join('\0', Catalog.Compare) + "\0"))
                partyCmp = Catalog.Compare[cmp];
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            ImGui.InputInt("Members", ref partyN);
            partyN = Math.Clamp(partyN, 0, 24);
            chipValue = partyCmp == "=" ? partyN.ToString() : partyCmp + partyN;
            return;
        }

        if (opts.Length > 0)
        {
            var idx = Array.FindIndex(opts, o => o.Equals(chipValue, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;
            ImGui.SetNextItemWidth(220);
            var labels = kind == ChipKind.Duty ? opts.Select(Catalog.DutyLabel).ToArray() : opts;
            if (ImGui.Combo("Value", ref idx, string.Join('\0', labels) + "\0"))
                chipValue = opts[idx];
            if (string.IsNullOrEmpty(chipValue)) chipValue = opts[0];
        }

        if (Catalog.UsesCustom(kind) && kind != ChipKind.PartySize)
        {
            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##custom", Catalog.HintFor(kind), ref chipValue, 48);
        }
    }

    private string DraftLabel()
    {
        var kind = (ChipKind)chipKind;
        var value = chipValue.Trim();
        return string.IsNullOrEmpty(value) ? kind + " — pick a value" : new RuleChip { Kind = kind, Value = value }.Label;
    }

    private void AddChip(Configuration cfg, List<RuleChip> list)
    {
        if (string.IsNullOrWhiteSpace(chipValue)) return;
        list.Add(new RuleChip { Kind = (ChipKind)chipKind, Value = chipValue.Trim() });
        cfg.Save();
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
            var label = chip.Label + "  ×";
            var w = ImGui.CalcTextSize(label).X + 16;
            if (ImGui.GetCursorPosX() - x0 + w > wrap) ImGui.NewLine();
            ImGui.PushID(chip.Kind + chip.Value);
            var live = ChipEval.ChipTrue(chip, snap);
            if (live) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.12f, 0.42f, 0.42f, 1f));
            if (ImGui.SmallButton(label)) drop = chip;
            if (live) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(live ? "True now — click to remove" : "False now — click to remove");
            ImGui.SameLine();
            ImGui.PopID();
        }
        if (chips.Count == 0) ImGui.TextDisabled("None");
        else ImGui.NewLine();
        if (drop is not null) { chips.Remove(drop); cfg.Save(); }
        ImGui.PopID();
    }

    private void DrawThenPicker()
    {
        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Action", ref thenKind, string.Join('\0', Catalog.ThenKinds) + "\0");
        var kind = (ThenKind)thenKind;
        if (kind == ThenKind.Command)
        {
            ImGui.SetNextItemWidth(320);
            ImGui.InputTextWithHint("##tv", "/command to send", ref thenValue, 180);
        }
        else if (kind == ThenKind.Wait)
        {
            ImGui.SetNextItemWidth(120);
            ImGui.InputInt("Milliseconds", ref waitMs);
            waitMs = Math.Clamp(waitMs, 0, 60000);
        }
        else if (kind == ThenKind.Status)
        {
            var idx = Array.FindIndex(Catalog.Statuses, s => s.Equals(thenValue, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;
            ImGui.SetNextItemWidth(220);
            if (ImGui.Combo("Status", ref idx, string.Join('\0', Catalog.Statuses) + "\0"))
                thenValue = Catalog.Statuses[idx];
            if (string.IsNullOrEmpty(thenValue)) thenValue = Catalog.Statuses[0];
        }
        else if (kind == ThenKind.Notify)
        {
            ImGui.SetNextItemWidth(320);
            ImGui.InputTextWithHint("##nv", "Line to print in chat", ref thenValue, 120);
        }
        else
        {
            ImGui.SetNextItemWidth(120);
            ImGui.Combo("Section", ref configSection, "System\0UI\0");
            var names = configSection == 0 ? Catalog.SystemOptions : Catalog.UiOptions;
            var gIdx = Array.IndexOf(Catalog.ConfigGroups, configGroup);
            if (gIdx < 0) gIdx = 0;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(160);
            if (ImGui.Combo("Group", ref gIdx, string.Join('\0', Catalog.ConfigGroups) + "\0"))
                configGroup = Catalog.ConfigGroups[gIdx];
            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint("##oq", "Search setting name", ref optionQuery, 48);
            var hits = Catalog.Filter(Catalog.InGroup(names, configGroup), optionQuery).Take(16).ToArray();
            if (hits.Length == 0) hits = Catalog.Filter(names, optionQuery).Take(16).ToArray();
            if (optionPick >= hits.Length) optionPick = 0;
            ImGui.SetNextItemWidth(280);
            if (hits.Length > 0 && ImGui.Combo("Setting", ref optionPick, string.Join('\0', hits) + "\0"))
                pickedOption = hits[optionPick];
            if (hits.Length > 0 && string.IsNullOrEmpty(pickedOption)) pickedOption = hits[0];
            ImGui.SetNextItemWidth(120);
            var onoff = Array.IndexOf(Catalog.ValuesOnOff, thenValue);
            if (onoff >= 0)
            {
                if (ImGui.Combo("Value", ref onoff, "On\0Off\0")) thenValue = Catalog.ValuesOnOff[onoff];
            }
            else
                ImGui.InputTextWithHint("##ov", "on / off / number", ref thenValue, 24);
            if (!string.IsNullOrWhiteSpace(pickedOption))
                ImGui.TextDisabled("Will set " + pickedOption + " = " + (string.IsNullOrWhiteSpace(thenValue) ? "(need a value)" : thenValue));
        }
    }

    private void AddThen(Configuration cfg, ThenRule rule)
    {
        var kind = (ThenKind)thenKind;
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
            _ => new ThenRow { Kind = ThenKind.Command, Value = thenValue.Trim() },
        });
        cfg.Save();
    }

    private static void DrawThenList(Configuration cfg, ThenRule rule)
    {
        ThenRow? drop = null;
        for (var i = 0; i < rule.Then.Count; i++)
        {
            var row = rule.Then[i];
            ImGui.PushID(i);
            if (ImGui.SmallButton(row.Label + "  ×")) drop = row;
            ImGui.SameLine();
            ImGui.PopID();
        }
        if (rule.Then.Count == 0) ImGui.TextDisabled("None");
        else ImGui.NewLine();
        if (drop is not null) { rule.Then.Remove(drop); cfg.Save(); }
    }
}
