using System;
using System.Collections.Generic;
using System.Linq;

namespace AndThen;

internal sealed class Engine
{
    private readonly Configuration cfg;
    private readonly HashSet<string> lastTrue = [];
    private readonly HashSet<string> asked = [];
    private readonly HashSet<string> firedStretch = [];
    private readonly Queue<Pending> pending = new();
    private readonly Dictionary<string, DateTime> justApplied = [];
    private DateTime nextDue = DateTime.MinValue;

    public GameSnapshot LastSnap { get; private set; } = new();
    public List<ThenRule> LastMatches { get; } = [];
    public List<ThenRule> DialogQueue { get; } = [];
    public List<AppliedLine> Log { get; } = [];
    public bool DialogOpenNeeded { get; set; }

    public Engine(Configuration cfg) => this.cfg = cfg;

    public void Reset()
    {
        lastTrue.Clear();
        asked.Clear();
        firedStretch.Clear();
        pending.Clear();
        LastMatches.Clear();
        DialogQueue.Clear();
        DialogOpenNeeded = false;
    }

    public bool JustApplied(string id) =>
        justApplied.TryGetValue(id, out var at) && DateTime.Now - at < TimeSpan.FromSeconds(2);

    public void Tick(bool force)
    {
        if (!cfg.Enabled && !force) return;
        if (!Plugin.CharacterReady) return;

        DrainPending();
        var interval = TimeSpan.FromSeconds(Math.Clamp(cfg.PollSec, 0.25, 5));
        if (!force && DateTime.Now < nextDue) return;
        nextDue = DateTime.Now + interval;

        var snap = GameSnapshot.Capture();
        LastSnap = snap;
        LastMatches.Clear();

        var quiet = Quiet(snap);
        var seen = new HashSet<string>();
        var dialogNow = new List<ThenRule>();

        foreach (var rule in LiveRules())
        {
            var match = ChipEval.Matches(rule, snap);
            if (!match)
            {
                firedStretch.Remove(rule.Id);
                continue;
            }

            LastMatches.Add(rule);
            seen.Add(rule.Id);

            if (force)
            {
                Enqueue(rule, "apply");
                firedStretch.Add(rule.Id);
                continue;
            }

            if (firedStretch.Contains(rule.Id))
                continue;

            if (quiet) continue;

            if (rule.Mode == ApplyMode.Auto)
            {
                Enqueue(rule, "auto");
                firedStretch.Add(rule.Id);
            }
            else if (rule.Mode == ApplyMode.Dialog)
                dialogNow.Add(rule);
        }

        lastTrue.Clear();
        foreach (var id in seen) lastTrue.Add(id);

        var dialogIds = new HashSet<string>(dialogNow.Select(r => r.Id));
        if (dialogNow.Count > 0 && !dialogIds.SetEquals(asked))
        {
            DialogQueue.Clear();
            DialogQueue.AddRange(dialogNow);
            asked.Clear();
            foreach (var id in dialogIds) asked.Add(id);
            DialogOpenNeeded = true;
        }
        if (dialogNow.Count == 0)
        {
            asked.Clear();
            DialogQueue.Clear();
        }
    }

    public void Test(ThenRule rule)
    {
        if (!Plugin.CharacterReady) return;
        LastSnap = GameSnapshot.Capture();
        Enqueue(rule, "command");
        DrainPending();
    }

    public void AcceptDialog(ThenRule rule)
    {
        Enqueue(rule, "dialog");
        firedStretch.Add(rule.Id);
        DialogQueue.RemoveAll(r => r.Id == rule.Id);
        DrainPending();
    }

    public void DismissDialog()
    {
        foreach (var rule in DialogQueue)
            firedStretch.Add(rule.Id);
        DialogQueue.Clear();
        DialogOpenNeeded = false;
    }

    public IEnumerable<string> Preview(ThenRule rule) =>
        rule.Then.Select(r => r.WaitMs > 0 ? $"{r.WaitMs} ms · {r.Label}" : r.Label);

    public IEnumerable<string> Why(ThenRule rule, GameSnapshot snap)
    {
        yield return ChipEval.Matches(rule, snap) ? "MATCH" : "no match";
        foreach (var chip in rule.AndChips)
            yield return $"IF {(ChipEval.ChipTrue(chip, snap) ? "Y" : "n")}  {chip.Label}";
        foreach (var chip in rule.OrChips)
            yield return $"OR {(ChipEval.ChipTrue(chip, snap) ? "Y" : "n")}  {chip.Label}";
        foreach (var chip in rule.NotChips)
            yield return $"NOT {(ChipEval.ChipTrue(chip, snap) ? "Y" : "n")}  {chip.Label}";
        if (rule.AndChips.Count + rule.OrChips.Count + rule.NotChips.Count == 0)
            yield return "(no chips — /atn Name still runs THEN)";
    }

    private bool Quiet(GameSnapshot snap)
    {
        if (cfg.QuietInCutscene && (snap.States.Contains("Cutscene") || snap.States.Contains("WatchingCutscene"))) return true;
        if (cfg.QuietInCombat && snap.States.Contains("InCombat")) return true;
        if (cfg.QuietInDuty && snap.States.Contains("InDuty")) return true;
        if (cfg.QuietBetweenAreas && snap.States.Contains("BetweenAreas")) return true;
        if (cfg.QuietWhenOccupied && snap.States.Contains("Occupied")) return true;
        if (cfg.QuietInGPose && snap.States.Contains("GPose")) return true;
        if (cfg.QuietWhenDead && snap.States.Contains("Dead")) return true;
        if (cfg.QuietWhenCrafting && snap.States.Contains("Crafting")) return true;
        if (cfg.QuietWhenPerforming && snap.States.Contains("Performing")) return true;
        if (cfg.QuietWhenTrading && snap.States.Contains("Trade")) return true;
        return false;
    }

    private IEnumerable<ThenRule> LiveRules()
    {
        foreach (var rule in cfg.Rules)
        {
            if (!rule.Enabled) continue;
            yield return rule;
        }
    }

    private void Enqueue(ThenRule rule, string how)
    {
        if (rule.Then.Count == 0) return;
        pending.Enqueue(new Pending { RuleName = rule.Name, RuleId = rule.Id, Rows = [.. rule.Then], Index = 0, Due = DateTime.Now });
        justApplied[rule.Id] = DateTime.Now;
        AddLog(rule.Name, how);
        Plugin.Notify($"[{rule.Name}] {how}");
    }

    private void DrainPending()
    {
        var guard = 0;
        while (pending.Count > 0 && guard++ < 64)
        {
            var item = pending.Peek();
            if (DateTime.Now < item.Due) return;
            if (item.Index >= item.Rows.Count)
            {
                pending.Dequeue();
                continue;
            }

            var row = item.Rows[item.Index];
            if (!item.Waited)
            {
                item.Waited = true;
                var ms = row.Kind == ThenKind.Wait
                    ? (row.WaitMs > 0 ? row.WaitMs : ParseWait(row.Value))
                    : Math.Max(row.WaitMs, 0);
                if (row.Kind == ThenKind.Config && ms < 50) ms = 50;
                if (row.Kind == ThenKind.Logout && ms < 500) ms = 500;
                if (row.Kind == ThenKind.Exit && ms < 1000) ms = 1000;
                if (ms > 0)
                {
                    item.Due = DateTime.Now.AddMilliseconds(ms);
                    return;
                }
            }

            item.Index++;
            item.Waited = false;
            if (row.Kind != ThenKind.Wait)
                Actions.Run(row);
        }
    }

    private void AddLog(string name, string how)
    {
        Log.Insert(0, new AppliedLine { At = DateTime.Now, Name = name, Detail = how });
        if (Log.Count > 10) Log.RemoveAt(Log.Count - 1);
    }

    private static int ParseWait(string value)
    {
        if (int.TryParse(value, out var n)) return n >= 1000 ? n : n * 1000;
        return 0;
    }

    private sealed class Pending
    {
        public string RuleName { get; init; } = string.Empty;
        public string RuleId { get; init; } = string.Empty;
        public List<ThenRow> Rows { get; init; } = [];
        public int Index { get; set; }
        public DateTime Due { get; set; }
        public bool Waited { get; set; }
    }
}
