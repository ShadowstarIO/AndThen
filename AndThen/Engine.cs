using System;
using System.Collections.Generic;
using System.Linq;

namespace AndThen;

internal sealed class Engine
{
    private readonly Configuration cfg;
    private readonly HashSet<string> lastTrue = [];
    private readonly HashSet<string> asked = [];
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

        var quietAuto = cfg.QuietInCutscene && snap.States.Contains("Cutscene");
        var seen = new HashSet<string>();
        var dialogNow = new List<ThenRule>();

        foreach (var rule in LiveRules())
        {
            var match = ChipEval.Matches(rule, snap);
            if (!match) continue;
            LastMatches.Add(rule);
            seen.Add(rule.Id);
            var rising = force || !lastTrue.Contains(rule.Id);
            if (!rising) continue;

            if (force)
            {
                Enqueue(rule, "apply");
                continue;
            }

            if (rule.Mode == ApplyMode.Auto && !quietAuto)
                Enqueue(rule, "auto");
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
        DialogQueue.RemoveAll(r => r.Id == rule.Id);
        DrainPending();
    }

    public void DismissDialog()
    {
        DialogQueue.Clear();
        DialogOpenNeeded = false;
    }

    public IEnumerable<string> Preview(ThenRule rule) => rule.Then.Select(r => r.Label);

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

    private IEnumerable<ThenRule> LiveRules()
    {
        foreach (var rule in cfg.Rules)
        {
            if (!rule.Enabled) continue;
            if (cfg.MutedFolders.Contains(rule.FolderKey)) continue;
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
            item.Index++;
            if (row.Kind == ThenKind.Wait)
            {
                var ms = row.WaitMs > 0 ? row.WaitMs : ParseWait(row.Value);
                item.Due = DateTime.Now.AddMilliseconds(Math.Max(0, ms));
                return;
            }

            Actions.Run(row);
            if (row.Kind == ThenKind.Config)
            {
                item.Due = DateTime.Now.AddMilliseconds(80);
                return;
            }
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
    }
}
