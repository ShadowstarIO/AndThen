using System;
using System.Collections.Generic;

namespace AndThen;

internal sealed class Engine
{
    private readonly Configuration cfg;
    private readonly HashSet<string> lastTrue = [];
    private readonly Queue<Pending> pending = new();
    private DateTime nextDue = DateTime.MinValue;

    public GameSnapshot LastSnap { get; private set; } = new();
    public List<ThenRule> LastMatches { get; } = [];

    public Engine(Configuration cfg) => this.cfg = cfg;

    public void Reset()
    {
        lastTrue.Clear();
        pending.Clear();
        LastMatches.Clear();
    }

    public void Tick(bool force)
    {
        if (!cfg.Enabled && !force) return;
        if (!Plugin.CharacterReady) return;

        DrainPending();
        if (!force && DateTime.Now < nextDue) return;
        nextDue = DateTime.Now.AddMilliseconds(Math.Max(100, cfg.PollMs));

        var snap = GameSnapshot.Capture();
        LastSnap = snap;
        LastMatches.Clear();

        var seen = new HashSet<string>();
        foreach (var rule in Sorted())
        {
            if (!rule.Enabled && !force) continue;
            var match = ChipEval.Matches(rule, snap);
            if (!match) continue;
            LastMatches.Add(rule);
            seen.Add(rule.Id);
            var rising = force || !lastTrue.Contains(rule.Id);
            if (rising) Enqueue(rule);
        }

        lastTrue.Clear();
        foreach (var id in seen) lastTrue.Add(id);
    }

    public void Test(ThenRule rule)
    {
        if (!Plugin.CharacterReady) return;
        LastSnap = GameSnapshot.Capture();
        Enqueue(rule);
        DrainPending();
    }

    private void Enqueue(ThenRule rule)
    {
        if (rule.Then.Count == 0) return;
        pending.Enqueue(new Pending { RuleName = rule.Name, Rows = [.. rule.Then], Index = 0, Due = DateTime.Now });
        Plugin.Notify($"[{rule.Name}] running {rule.Then.Count} action(s)");
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
        }
    }

    private List<ThenRule> Sorted()
    {
        var copy = new List<ThenRule>(cfg.Rules);
        copy.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        return copy;
    }

    private static int ParseWait(string value)
    {
        if (int.TryParse(value, out var n)) return n >= 1000 ? n : n * 1000;
        return 0;
    }

    private sealed class Pending
    {
        public string RuleName { get; init; } = string.Empty;
        public List<ThenRow> Rows { get; init; } = [];
        public int Index { get; set; }
        public DateTime Due { get; set; }
    }
}
