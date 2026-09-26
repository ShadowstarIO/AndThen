using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AndThen.Windows;

public sealed partial class MainWindow
{
    private void DrawEditor(Configuration cfg, ThenRule rule)
    {
        if (IconButton(FontAwesomeIcon.Play, "tthen", "Test THEN now")) plugin.TestRule(rule);
        ImGui.SameLine();
        if (IconButton(FontAwesomeIcon.Clone, "tdup", "Duplicate")) plugin.DuplicateRule(rule);
        ImGui.SameLine();
        if (IconButton(FontAwesomeIcon.Share, "tat1", "Copy AT1 share code")) { ImGui.SetClipboardText(Share.Encode(rule)); importMsg = "Copied share code."; }
        ImGui.SameLine();
        if (IconButton(FontAwesomeIcon.Copy, "tjson", "Copy JSON")) { ImGui.SetClipboardText(Share.ToJson(rule)); importMsg = "Copied JSON."; }
        ImGui.SameLine();
        if (IconButton(FontAwesomeIcon.Question, "twhy", "Why — print chip results")) foreach (var line in plugin.Engine.Why(rule, plugin.Snapshot())) Plugin.Notify(line);
        ImGui.SameLine();
        if (IconButton(FontAwesomeIcon.Paste, "tpaste", "Paste rule(s) from clipboard"))
            importMsg = plugin.TryImport(ImGui.GetClipboardText() ?? string.Empty, out var err) ? "Imported." : err;
        ImGui.TextDisabled($"/atn {rule.CommandToken} always runs this rule. Teal chip = true right now.");

        var on = rule.Enabled;
        if (ImGui.Checkbox("Enabled", ref on)) { rule.Enabled = on; cfg.Save(); }
        ImGui.SameLine();
        var name = rule.Name;
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputText("##name", ref name, 64) && name != rule.Name) { rule.Name = name; cfg.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Name");
        ImGui.SameLine();
        var notes = rule.Notes;
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputTextWithHint("##note", "Note — also the Dialog text", ref notes, 160) && notes != rule.Notes) { rule.Notes = notes; cfg.Save(); }
        ImGui.SameLine();
        var mode = (int)rule.Mode;
        ImGui.SetNextItemWidth(90);
        if (ImGui.Combo("##mode", ref mode, "Off\0Dialog\0Auto\0"))
        {
            rule.Mode = (ApplyMode)mode;
            cfg.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Off / Dialog / Auto");
        ImGui.SameLine();
        var wait = rule.DelaySec;
        ImGui.SetNextItemWidth(60);
        if (ImGui.InputFloat("##wait", ref wait, 0, 0, "%.1f"))
        {
            rule.DelaySec = Math.Clamp(wait, 0, 120);
            cfg.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("wait s");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Seconds the condition must stay true before Auto or Dialog fires. 0 = immediately.");

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

    private void ClearChipDraft()
    {
        chipValue = string.Empty;
        partyCmp = ">=";
        partyN = 4;
        mountName = string.Empty;
        mountPick = 0;
        levelCmp = ">=";
        levelN = 90;
        houseKind = 0;
        houseZone = 0;
        houseSub = false;
        houseWard = 0;
        housePlot = 0;
        houseRoom = 0;
    }

    private void ClearThenDraft()
    {
        thenValue = string.Empty;
        optionQuery = string.Empty;
        pickedOption = string.Empty;
        optionPick = 0;
        waitMs = 250;
        configSection = 0;
        configGroup = "Graphics";
        penEnabled = true;
        penInherit = false;
        penPermanent = false;
        penPriority = 0;
        penDir = string.Empty;
        penExtra = string.Empty;
        penSearch = string.Empty;
        penGroups = 0;
    }

    private void DrawChipPicker()
    {
        ImGui.SetNextItemWidth(180);
        if (ImGui.Combo("Kind", ref chipKind, string.Join('\0', Catalog.ChipKinds) + "\0"))
        {
            ClearChipDraft();
            lastChipKind = chipKind;
        }
        if (chipKind != lastChipKind)
        {
            ClearChipDraft();
            lastChipKind = chipKind;
        }

        var kind = (ChipKind)chipKind;
        var snap = plugin.Snapshot();
        var current = Catalog.CurrentFor(kind, snap);

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
            CurrentButton(current, v => { chipValue = v; if (int.TryParse(v, out var n)) partyN = n; });
            return;
        }

        if (kind == ChipKind.Level)
        {
            ImGui.SetNextItemWidth(80);
            var cmp = Array.IndexOf(Catalog.Compare, levelCmp);
            if (cmp < 0) cmp = 0;
            if (ImGui.Combo("Compare", ref cmp, string.Join('\0', Catalog.Compare) + "\0"))
                levelCmp = Catalog.Compare[cmp];
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            ImGui.InputInt("Level", ref levelN);
            levelN = Math.Clamp(levelN, 1, 100);
            chipValue = levelCmp == "=" ? levelN.ToString() : levelCmp + levelN;
            CurrentButton(current, v => { chipValue = v; if (int.TryParse(v, out var n)) levelN = n; });
            return;
        }

        if (kind == ChipKind.Mount)
        {
            DrawMountDraft(snap);
            return;
        }

        if (kind == ChipKind.Housing)
        {
            DrawHousingDraft(snap);
            return;
        }

        var opts = Catalog.OptionsFor(kind);
        if (opts.Length > 0) DrawOptionList(kind, opts, current);
        if (Catalog.UsesCustom(kind) && kind != ChipKind.PartySize)
        {
            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##custom", Catalog.HintFor(kind), ref chipValue, 48);
        }
    }

    private void DrawOptionList(ChipKind kind, string[] opts, string current)
    {
        CurrentButton(current, v => chipValue = v);
        var labels = kind == ChipKind.Duty ? opts.Select(Catalog.DutyLabel).ToArray() : opts;
        if (Catalog.LongList(kind) || opts.Length > 16)
        {
            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##searchv", "Search / type a value", ref chipValue, 48);
            ImGui.SameLine();
            if (ImGui.Button("Browse"))
                plugin.OpenPicker(Catalog.ChipKinds[(int)kind], opts, v => chipValue = v, current);
            foreach (var hit in Catalog.Filter(opts, chipValue).Take(8))
            {
                ImGui.SameLine();
                if (ImGui.SmallButton(hit)) chipValue = hit;
            }
            return;
        }

        var idx = Array.FindIndex(opts, o => o.Equals(chipValue, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) idx = 0;
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("Value", ref idx, string.Join('\0', labels) + "\0"))
            chipValue = opts[idx];
        if (string.IsNullOrEmpty(chipValue) && opts.Length > 0) chipValue = opts[0];
    }

    private void CurrentButton(string current, Action<string> set)
    {
        if (string.IsNullOrWhiteSpace(current)) return;
        if (ImGui.SmallButton("current: " + current + " [+]")) set(current);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Use what is true right now");
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
        if (ImGui.Combo("Action", ref thenKind, string.Join('\0', Catalog.ThenKinds) + "\0"))
        {
            ClearThenDraft();
            lastThenKind = thenKind;
        }
        if (thenKind != lastThenKind)
        {
            ClearThenDraft();
            lastThenKind = thenKind;
        }

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
            CurrentButton(plugin.Snapshot().OnlineStatus, v => thenValue = v);
        }
        else if (kind == ThenKind.Notify)
        {
            ImGui.SetNextItemWidth(320);
            ImGui.InputTextWithHint("##nv", "Line to print in chat", ref thenValue, 120);
        }
        else if (kind == ThenKind.Logout || kind == ThenKind.Exit)
        {
            ImGui.TextDisabled(kind == ThenKind.Logout
                ? "Sends /logout. Minimum wait 500 ms when the stack runs."
                : "Closes the game process. Minimum wait 1000 ms when the stack runs.");
        }
        else if (kind == ThenKind.PenumbraMod)
            DrawPenumbraModDraft();
        else if (kind == ThenKind.PenumbraReset)
            DrawPenumbraResetDraft();
        else
            DrawConfigDraft();
    }

    private void DrawMountDraft(GameSnapshot snap)
    {
        ImGui.SetNextItemWidth(160);
        if (ImGui.Combo("Mount", ref mountPick, "Not mounted\0Any mount\0Flying\0"))
            mountName = string.Empty;
        mountPick = Math.Clamp(mountPick, 0, 2);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        ImGui.InputTextWithHint("##mountname", "Or a mount name", ref mountName, 48);
        chipValue = mountName.Trim().Length > 0 ? mountName.Trim() : Catalog.Mounts[mountPick];
        var current = snap.Mount != "none" ? snap.Mount : snap.States.Contains("Mounted") ? "any" : "none";
        CurrentButton(current, v =>
        {
            mountName = v is "none" or "any" or "flying" ? string.Empty : v;
            mountPick = v switch { "any" => 1, "flying" => 2, "none" => 0, _ => mountPick };
            chipValue = v;
        });
    }

    private void DrawHousingDraft(GameSnapshot snap)
    {
        ImGui.SetNextItemWidth(140);
        ImGui.Combo("Place", ref houseKind, "House\0Apartment\0Chamber\0");
        houseKind = Math.Clamp(houseKind, 0, 2);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        ImGui.Combo("District", ref houseZone, "Any district\0Mist\0The Lavender Beds\0The Goblet\0Shirogane\0Empyreum\0");
        houseZone = Math.Clamp(houseZone, 0, Housing.Districts.Length);
        ImGui.SameLine();
        ImGui.Checkbox("Subdivision", ref houseSub);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Must match. A main-division address does not match the subdivision.");

        ImGui.SetNextItemWidth(90);
        ImGui.InputInt("Ward", ref houseWard);
        houseWard = Math.Clamp(houseWard, 0, 30);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("1–30. 0 means any ward.");
        if (houseKind != 1)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.InputInt("Plot", ref housePlot);
            housePlot = Math.Clamp(housePlot, 0, 60);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The plot number the client reports, 1–60. 0 means any plot. Subdivision is the checkbox, not a plot number.");
        }
        else housePlot = 0;

        if (houseKind != 0)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.InputInt(houseKind == 1 ? "Room" : "Room", ref houseRoom);
            houseRoom = Math.Clamp(houseRoom, 0, 9999);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Apartment or chamber number. 0 means any room. This is not a plot.");
        }
        else houseRoom = 0;

        chipValue = Housing.Encode(houseKind, houseZone, houseSub, houseWard, housePlot, houseRoom);
        ImGui.TextDisabled(snap.Address.Summary);
        if (snap.Address.Kind != ResidenceKind.None && ImGui.SmallButton("Use this address"))
        {
            houseKind = snap.Address.Kind switch
            {
                ResidenceKind.Apartment => 1,
                ResidenceKind.Chamber => 2,
                _ => 0,
            };
            houseZone = Housing.ZoneIndex(snap.Address.District);
            houseSub = snap.Address.Subdivision;
            houseWard = snap.Address.Ward;
            housePlot = snap.Address.Plot;
            houseRoom = snap.Address.Room;
            chipValue = Housing.Encode(snap.Address);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Ward is 1–30. Apartment wings use the room number, not a plot.");
    }

    private void DrawConfigDraft()
    {
        var gIdx = Array.IndexOf(Catalog.ConfigGroups, configGroup);
        if (gIdx < 0) gIdx = 0;
        ImGui.SetNextItemWidth(180);
        if (ImGui.Combo("Group", ref gIdx, string.Join('\0', Catalog.ConfigGroups) + "\0"))
        {
            configGroup = Catalog.ConfigGroups[gIdx];
            pickedOption = string.Empty;
            optionQuery = string.Empty;
            optionPick = 0;
            thenValue = string.Empty;
        }

        if (configGroup == "All settings")
        {
            DrawAllSettings();
            return;
        }

        var entries = SettingGuide.InGroup(configGroup)
            .Where(e => optionQuery.Length == 0
                || e.Label.Contains(optionQuery, StringComparison.OrdinalIgnoreCase)
                || e.Option.Contains(optionQuery, StringComparison.OrdinalIgnoreCase)
                || e.About.Contains(optionQuery, StringComparison.OrdinalIgnoreCase))
            .Take(24)
            .ToArray();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##oq", "Search", ref optionQuery, 48);
        if (entries.Length == 0)
        {
            ImGui.TextDisabled("No settings in this group match.");
            return;
        }

        var labels = entries.Select(e => e.Label).ToArray();
        if (optionPick >= entries.Length) optionPick = 0;
        if (string.IsNullOrEmpty(pickedOption) || entries.All(e => e.Option != pickedOption))
        {
            optionPick = 0;
            pickedOption = entries[0].Option;
        }
        else optionPick = Array.FindIndex(entries, e => e.Option == pickedOption);

        ImGui.SetNextItemWidth(320);
        if (ImGui.Combo("Setting", ref optionPick, string.Join('\0', labels) + "\0"))
        {
            pickedOption = entries[optionPick].Option;
            thenValue = string.Empty;
        }

        var info = entries[Math.Clamp(optionPick, 0, entries.Length - 1)];
        pickedOption = info.Option;
        configSection = info.Section == ConfigSection.System ? 0 : 1;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(info.Menu + "\n" + info.About);
        ImGui.TextWrapped(info.About);
        ImGui.TextDisabled(info.Menu + "  ·  " + info.Option);
        DrawSettingValue(info);
    }

    private void DrawAllSettings()
    {
        ImGui.SetNextItemWidth(110);
        if (ImGui.Combo("Section", ref configSection, "System\0UI\0"))
        {
            pickedOption = string.Empty;
            optionQuery = string.Empty;
            optionPick = 0;
        }
        var names = configSection == 0 ? Catalog.SystemOptions : Catalog.UiOptions;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##oq", "Search internal name", ref optionQuery, 48);
        ImGui.SameLine();
        if (ImGui.Button("Browse##cfg"))
            plugin.OpenPicker("Game setting", names.ToArray(), v => pickedOption = v, pickedOption);
        var hits = Catalog.Filter(names, optionQuery).Take(16).ToArray();
        if (optionPick >= hits.Length) optionPick = 0;
        ImGui.SetNextItemWidth(320);
        if (hits.Length > 0 && ImGui.Combo("Setting", ref optionPick, string.Join('\0', hits) + "\0"))
            pickedOption = hits[optionPick];
        if (hits.Length > 0 && string.IsNullOrEmpty(pickedOption)) pickedOption = hits[0];
        var info = SettingGuide.Find(pickedOption);
        if (info is not null)
        {
            ImGui.TextWrapped(info.About);
            ImGui.TextDisabled(info.Menu);
            DrawSettingValue(info);
            return;
        }

        var nowVal = Catalog.CurrentSetting(configSection == 0 ? ConfigSection.System : ConfigSection.Ui, pickedOption);
        if (!string.IsNullOrEmpty(nowVal) && ImGui.SmallButton("current: " + nowVal + " [+]")) thenValue = nowVal;
        ImGui.SetNextItemWidth(160);
        ImGui.InputTextWithHint("##ov", "Number the game stores", ref thenValue, 24);
        ImGui.TextDisabled("Internal name. Unknown words are not written. Graphics names also write the DirectX 11 twin when one exists.");
    }

    private void DrawSettingValue(SettingInfo info)
    {
        var nowVal = Catalog.CurrentSetting(info.Section, info.Option);
        var shown = SettingGuide.Display(info.Option, nowVal);
        if (!string.IsNullOrEmpty(nowVal) && ImGui.SmallButton("current: " + shown + " [+]")) thenValue = nowVal;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Use the value the client has right now.");

        if (info.Kind == SettingKind.Choice && info.Choices.Length > 0)
        {
            var labels = info.Choices.Select(c => c.Value + "  " + c.Label).ToArray();
            var idx = Array.FindIndex(info.Choices, c => c.Value == thenValue);
            if (idx < 0) idx = 0;
            ImGui.SetNextItemWidth(280);
            if (ImGui.Combo("Value", ref idx, string.Join('\0', labels) + "\0"))
                thenValue = info.Choices[idx].Value;
            if (string.IsNullOrEmpty(thenValue)) thenValue = info.Choices[0].Value;
            return;
        }

        if (info.Kind == SettingKind.Toggle)
        {
            if (string.IsNullOrEmpty(thenValue)) thenValue = "1";
            var idx = thenValue == "0" ? 1 : 0;
            ImGui.SetNextItemWidth(120);
            if (ImGui.Combo("Value", ref idx, "On\0Off\0")) thenValue = idx == 0 ? "1" : "0";
            return;
        }

        if (info.Kind == SettingKind.Volume)
        {
            var n = int.TryParse(thenValue, out var parsed) ? parsed : info.Min;
            ImGui.SetNextItemWidth(180);
            if (ImGui.SliderInt("Value", ref n, info.Min, info.Max)) thenValue = n.ToString();
            if (string.IsNullOrEmpty(thenValue)) thenValue = n.ToString();
            return;
        }

        ImGui.SetNextItemWidth(160);
        var hint = info.Kind == SettingKind.Float ? "Decimal" : "Number the game stores";
        ImGui.InputTextWithHint("##ov", hint, ref thenValue, 24);
        if (configGroup == "Graphics")
            ImGui.TextDisabled("This also writes the matching standard or DirectX 11 value when both exist.");
    }

    private void DrawPenumbraModDraft()
    {
        if (!PenumbraIpc.Available)
        {
            ImGui.TextDisabled("Penumbra is not available. Install and enable it, then reopen this window.");
            return;
        }

        ImGui.SetNextItemWidth(280);
        ImGui.InputTextWithHint("##pmod", "Search installed mods", ref penSearch, 80);
        ImGui.SameLine();
        if (ImGui.Button("Browse##pmod"))
            plugin.OpenPicker("Penumbra mods", PenumbraIpc.Labels(), PickPenumbra, penDir);

        var hits = PenumbraIpc.Mods()
            .Where(m => penSearch.Length == 0
                || m.Name.Contains(penSearch, StringComparison.OrdinalIgnoreCase)
                || m.Dir.Contains(penSearch, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToArray();
        foreach (var hit in hits)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton(hit.Name)) PickPenumbraDir(hit.Dir, hit.Name);
        }

        if (penDir.Length == 0)
        {
            ImGui.TextDisabled("Pick a mod, then store its current collection settings.");
            return;
        }

        ImGui.TextUnformatted(thenValue);
        ImGui.SameLine();
        ImGui.TextDisabled(penDir);
        if (ImGui.Checkbox("Enabled", ref penEnabled)) { }
        ImGui.SameLine();
        if (ImGui.Checkbox("Inherit", ref penInherit)) { }
        ImGui.SameLine();
        if (ImGui.Checkbox("Permanent", ref penPermanent)) { }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Off = temporary settings on you (cleared on reset or logout).\nOn = write into the current collection.");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("Priority", ref penPriority);
        if (ImGui.Button("Use current settings"))
            CapturePenumbra();
        ImGui.SameLine();
        ImGui.TextDisabled(penGroups == 0 ? "No option groups stored yet." : penGroups + " option group(s) stored.");
    }

    private void DrawPenumbraResetDraft()
    {
        if (!PenumbraIpc.Available)
        {
            ImGui.TextDisabled("Penumbra is not available.");
            return;
        }

        ImGui.TextDisabled("Empty = clear every AndThen temporary setting on you. Or pick one mod.");
        ImGui.SetNextItemWidth(280);
        ImGui.InputTextWithHint("##preset", "Optional mod search", ref penSearch, 80);
        ImGui.SameLine();
        if (ImGui.Button("Browse##preset"))
            plugin.OpenPicker("Penumbra mods", PenumbraIpc.Labels(), PickPenumbra, penDir);
        if (penDir.Length > 0)
            ImGui.TextDisabled("Will reset temp settings for " + (string.IsNullOrWhiteSpace(thenValue) ? penDir : thenValue));
        else
            ImGui.TextDisabled("Will reset all AndThen temporary settings.");
        if (penDir.Length > 0 && ImGui.SmallButton("Clear pick"))
        {
            penDir = string.Empty;
            thenValue = string.Empty;
        }
    }

    private void PickPenumbra(string label)
    {
        if (!PenumbraIpc.TryParseLabel(label, out var dir, out var name)) return;
        PickPenumbraDir(dir, name);
    }

    private void PickPenumbraDir(string dir, string name)
    {
        penDir = dir;
        thenValue = name;
        penSearch = name;
        CapturePenumbra();
    }

    private void CapturePenumbra()
    {
        if (penDir.Length == 0) return;
        if (!PenumbraIpc.TryRead(penDir, thenValue, out var snap))
        {
            importMsg = "Could not read that mod from Penumbra.";
            return;
        }
        penEnabled = snap.Enabled;
        penInherit = snap.Inherit;
        penPriority = snap.Priority;
        penGroups = snap.GroupCount;
        penExtra = PenumbraIpc.Pack(snap);
        if (string.IsNullOrWhiteSpace(thenValue)) thenValue = snap.Name;
        importMsg = "Stored current settings for " + thenValue + ".";
    }

    private void AddThen(Configuration cfg, ThenRule rule)
    {
        var kind = (ThenKind)thenKind;
        if (kind == ThenKind.Command && string.IsNullOrWhiteSpace(thenValue)) return;
        if (kind == ThenKind.Config && (string.IsNullOrWhiteSpace(pickedOption) || string.IsNullOrWhiteSpace(thenValue))) return;
        if (kind == ThenKind.PenumbraMod && string.IsNullOrWhiteSpace(penDir)) return;
        rule.Then.Add(kind switch
        {
            ThenKind.Wait => new ThenRow { Kind = ThenKind.Wait, WaitMs = Math.Max(0, waitMs), Value = waitMs.ToString() },
            ThenKind.Status => new ThenRow { Kind = ThenKind.Status, Value = string.IsNullOrWhiteSpace(thenValue) ? "Busy" : thenValue.Trim() },
            ThenKind.Notify => new ThenRow { Kind = ThenKind.Notify, Value = string.IsNullOrWhiteSpace(thenValue) ? rule.Notes : thenValue.Trim() },
            ThenKind.Logout => new ThenRow { Kind = ThenKind.Logout, WaitMs = 500 },
            ThenKind.Exit => new ThenRow { Kind = ThenKind.Exit, WaitMs = 1000 },
            ThenKind.PenumbraMod => new ThenRow
            {
                Kind = ThenKind.PenumbraMod,
                Option = penDir,
                Value = thenValue.Trim(),
                Flag = penEnabled,
                Inherit = penInherit,
                Permanent = penPermanent,
                Number = penPriority,
                Extra = penExtra,
                WaitMs = 50,
            },
            ThenKind.PenumbraReset => new ThenRow
            {
                Kind = ThenKind.PenumbraReset,
                Option = penDir,
                Value = thenValue.Trim(),
                WaitMs = 50,
            },
            ThenKind.Config => new ThenRow
            {
                Kind = ThenKind.Config,
                Section = configSection == 0 ? ConfigSection.System : ConfigSection.Ui,
                Option = string.IsNullOrWhiteSpace(pickedOption) ? optionQuery.Trim() : pickedOption,
                Value = thenValue.Trim(),
                WaitMs = 50,
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
