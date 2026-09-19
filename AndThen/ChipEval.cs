using System;

namespace AndThen;

internal static class ChipEval
{
    public static bool Matches(ThenRule rule, GameSnapshot snap)
    {
        if (!AndOk(rule, snap)) return false;
        if (!OrOk(rule, snap)) return false;
        if (!NotOk(rule, snap)) return false;
        return true;
    }

    private static bool AndOk(ThenRule rule, GameSnapshot snap)
    {
        foreach (var chip in rule.AndChips)
        {
            if (!ChipTrue(chip, snap)) return false;
        }
        return true;
    }

    private static bool OrOk(ThenRule rule, GameSnapshot snap)
    {
        if (rule.OrChips.Count == 0) return true;
        foreach (var chip in rule.OrChips)
        {
            if (ChipTrue(chip, snap)) return true;
        }
        return false;
    }

    private static bool NotOk(ThenRule rule, GameSnapshot snap)
    {
        foreach (var chip in rule.NotChips)
        {
            if (ChipTrue(chip, snap)) return false;
        }
        return true;
    }

    public static bool ChipTrue(RuleChip chip, GameSnapshot snap)
    {
        var value = (chip.Value ?? string.Empty).Trim();
        return chip.Kind switch
        {
            ChipKind.State => snap.States.Contains(value),
            ChipKind.Job => snap.JobAbbr.Equals(value, StringComparison.OrdinalIgnoreCase)
                || snap.JobId.ToString().Equals(value, StringComparison.OrdinalIgnoreCase),
            ChipKind.Role => snap.Role.Equals(value, StringComparison.OrdinalIgnoreCase),
            ChipKind.Zone => snap.TerritoryName.Contains(value, StringComparison.OrdinalIgnoreCase)
                || snap.TerritoryId.ToString().Equals(value, StringComparison.OrdinalIgnoreCase),
            ChipKind.World => snap.WorldName.Equals(value, StringComparison.OrdinalIgnoreCase),
            ChipKind.DataCenter => snap.DataCenterName.Equals(value, StringComparison.OrdinalIgnoreCase),
            ChipKind.PartySize => PartyOk(value, snap.PartySize),
            ChipKind.Duty => DutyOk(value, snap),
            _ => false,
        };
    }

    private static bool PartyOk(string value, int size)
    {
        if (value.StartsWith(">=")) return int.TryParse(value[2..].Trim(), out var n) && size >= n;
        if (value.StartsWith("<=")) return int.TryParse(value[2..].Trim(), out var n) && size <= n;
        if (value.StartsWith('>')) return int.TryParse(value[1..].Trim(), out var n) && size > n;
        if (value.StartsWith('<')) return int.TryParse(value[1..].Trim(), out var n) && size < n;
        return int.TryParse(value, out var exact) && size == exact;
    }

    private static bool DutyOk(string value, GameSnapshot snap)
    {
        if (value.Equals("any", StringComparison.OrdinalIgnoreCase))
            return snap.States.Contains("InDuty");
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
            return !snap.States.Contains("InDuty");
        if (value.Equals("raid", StringComparison.OrdinalIgnoreCase))
            return snap.States.Contains("InDuty") && snap.PartySize >= 8;
        if (value.Equals("alliance", StringComparison.OrdinalIgnoreCase))
            return snap.States.Contains("InDuty") && snap.PartySize >= 24;
        if (value.Equals("light", StringComparison.OrdinalIgnoreCase) || value.Equals("dungeon", StringComparison.OrdinalIgnoreCase))
            return snap.States.Contains("InDuty") && snap.PartySize > 0 && snap.PartySize <= 4;
        return snap.States.Contains("InDuty") && snap.TerritoryName.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
