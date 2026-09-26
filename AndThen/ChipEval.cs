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
            if (!ChipTrue(chip, snap)) return false;
        return true;
    }

    private static bool OrOk(ThenRule rule, GameSnapshot snap)
    {
        if (rule.OrChips.Count == 0) return true;
        foreach (var chip in rule.OrChips)
            if (ChipTrue(chip, snap)) return true;
        return false;
    }

    private static bool NotOk(ThenRule rule, GameSnapshot snap)
    {
        foreach (var chip in rule.NotChips)
            if (ChipTrue(chip, snap)) return false;
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
            ChipKind.PartySize => Compare(value, snap.PartySize),
            ChipKind.Duty => DutyOk(value, snap),
            ChipKind.Group => snap.Group.Equals(value, StringComparison.OrdinalIgnoreCase),
            ChipKind.Time => TimeOk(value, snap),
            ChipKind.Weather => snap.Weather.Contains(value, StringComparison.OrdinalIgnoreCase),
            ChipKind.Nearby => NearbyOk(value, snap.Nearby),
            ChipKind.Place => snap.HasPlace(value),
            ChipKind.Target => TargetOk(value, snap.Target),
            ChipKind.OnlineStatus => snap.OnlineStatus.Equals(value, StringComparison.OrdinalIgnoreCase)
                || snap.OnlineStatus.Contains(value, StringComparison.OrdinalIgnoreCase),
            ChipKind.Account => AccountOk(value, snap),
            ChipKind.Housing => Housing.Matches(value, snap.Address),
            ChipKind.Mount => MountOk(value, snap),
            ChipKind.Level => Compare(value, snap.Level),
            _ => false,
        };
    }

    private static bool DutyOk(string value, GameSnapshot snap)
    {
        if (value.Equals("any", StringComparison.OrdinalIgnoreCase))
            return snap.States.Contains("InDuty") || snap.States.Contains("DutyReady") || snap.States.Contains("InQueue");
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
            return !snap.States.Contains("InDuty");
        if (value.Equals("solid", StringComparison.OrdinalIgnoreCase))
            return snap.States.Contains("InDuty");
        if (value.Equals("dungeon", StringComparison.OrdinalIgnoreCase) || value.Equals("light", StringComparison.OrdinalIgnoreCase))
            return snap.HasPlace("Dungeon") || (snap.States.Contains("InDuty") && snap.Group == "Light");
        if (value.Equals("trial", StringComparison.OrdinalIgnoreCase))
            return snap.HasPlace("Trial");
        if (value.Equals("raid", StringComparison.OrdinalIgnoreCase))
            return snap.HasPlace("Raid") || (snap.States.Contains("InDuty") && snap.Group is "Full" or "Alliance");
        if (value.Equals("alliance", StringComparison.OrdinalIgnoreCase))
            return snap.HasPlace("Alliance") || snap.Group.Equals("Alliance", StringComparison.OrdinalIgnoreCase);
        if (value.Equals("pvp", StringComparison.OrdinalIgnoreCase))
            return snap.HasPlace("PvP") || snap.States.Contains("PvP");
        if (value.Equals("deep", StringComparison.OrdinalIgnoreCase))
            return snap.HasPlace("DeepDungeon") || snap.States.Contains("DeepDungeon");
        if (value.Equals("field", StringComparison.OrdinalIgnoreCase))
            return snap.HasPlace("Field");
        return snap.States.Contains("InDuty") && snap.TerritoryName.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TimeOk(string value, GameSnapshot snap)
    {
        if (value.Equals(snap.Weekday, StringComparison.OrdinalIgnoreCase)) return true;
        var key = value.ToLowerInvariant();
        if (key is "day") return snap.EorzeaHour is >= 6 and < 18;
        if (key is "night") return snap.EorzeaHour is >= 18 or < 6;
        if (key is "dawn") return snap.EorzeaHour is >= 5 and < 8;
        if (key is "dusk") return snap.EorzeaHour is >= 17 and < 20;
        if (value.StartsWith("et", StringComparison.OrdinalIgnoreCase))
            return Compare(value[2..].Trim(), snap.EorzeaHour);
        if (value.StartsWith("lt", StringComparison.OrdinalIgnoreCase) || value.StartsWith("local", StringComparison.OrdinalIgnoreCase))
        {
            var rest = value.StartsWith("local", StringComparison.OrdinalIgnoreCase) ? value[5..].Trim() : value[2..].Trim();
            return Compare(rest, snap.LocalHour);
        }
        return Compare(value, snap.EorzeaHour);
    }

    private static bool NearbyOk(string value, int n) => value.ToLowerInvariant() switch
    {
        "empty" => n == 0,
        "few" => n is > 0 and <= 8,
        "crowded" => n > 8,
        _ => Compare(value, n),
    };

    private static bool AccountOk(string value, GameSnapshot snap)
    {
        if (value.Equals("home", StringComparison.OrdinalIgnoreCase)) return snap.Account == "Home";
        if (value.Equals("visiting", StringComparison.OrdinalIgnoreCase))
            return snap.Account == "Visiting";
        return snap.Account.Equals(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MountOk(string value, GameSnapshot snap)
    {
        var mounted = snap.States.Contains("Mounted");
        var flying = snap.States.Contains("Flying");
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) return !mounted;
        if (value.Equals("any", StringComparison.OrdinalIgnoreCase)) return mounted;
        if (value.Equals("flying", StringComparison.OrdinalIgnoreCase)) return flying;
        return mounted && snap.Mount.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TargetOk(string value, string target)
    {
        if (value.Equals("any", StringComparison.OrdinalIgnoreCase)) return target != "none";
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) return target == "none";
        return target.Equals(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Compare(string value, int n)
    {
        value = value.Trim();
        if (value.StartsWith(">=")) return int.TryParse(value[2..].Trim(), out var a) && n >= a;
        if (value.StartsWith("<=")) return int.TryParse(value[2..].Trim(), out var b) && n <= b;
        if (value.StartsWith('>')) return int.TryParse(value[1..].Trim(), out var c) && n > c;
        if (value.StartsWith('<')) return int.TryParse(value[1..].Trim(), out var d) && n < d;
        return int.TryParse(value, out var exact) && n == exact;
    }
}
