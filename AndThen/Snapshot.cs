using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace AndThen;

public sealed class GameSnapshot
{
    public DateTime Now { get; init; }
    public bool LoggedIn { get; init; }
    public uint TerritoryId { get; init; }
    public string TerritoryName { get; init; } = string.Empty;
    public uint IntendedUse { get; init; }
    public string Place { get; init; } = string.Empty;
    public string WorldName { get; init; } = string.Empty;
    public string DataCenterName { get; init; } = string.Empty;
    public uint JobId { get; init; }
    public string JobAbbr { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int PartySize { get; init; }
    public string Group { get; init; } = "Solo";
    public int Nearby { get; init; }
    public string Target { get; init; } = "none";
    public int LocalHour { get; init; }
    public int EorzeaHour { get; init; }
    public string Weekday { get; init; } = string.Empty;
    public string Weather { get; init; } = string.Empty;
    public HashSet<string> States { get; init; } = [];
    public HashSet<string> Places { get; init; } = [];

    public static GameSnapshot Capture()
    {
        var logged = Plugin.ClientState.IsLoggedIn && Plugin.ObjectTable.LocalPlayer is not null;
        var player = Plugin.ObjectTable.LocalPlayer;
        var job = player?.ClassJob.ValueNullable;
        var world = player?.CurrentWorld.ValueNullable;
        var dc = world?.DataCenter.ValueNullable;
        uint territoryId = Plugin.ClientState.TerritoryType;
        var territory = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
        var party = Plugin.PartyList.Length;
        var intended = territory?.TerritoryIntendedUse.RowId ?? 0;
        var allianceZone = intended is 8 or 41 or 48 or 61;
        var group = allianceZone || party >= 24 ? "Alliance"
            : party >= 5 ? "Full"
            : party >= 2 ? "Light"
            : "Solo";

        var nearby = 0;
        if (player is not null)
        {
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj.ObjectKind != ObjectKind.Pc || obj.EntityId == player.EntityId) continue;
                var d = obj.Position - player.Position;
                if (d.LengthSquared() <= 30f * 30f) nearby++;
            }
        }

        var now = DateTime.Now;
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var eorzeaHour = (int)((unix * 3600.0 / 175.0 / 3600.0) % 24);
        var places = PlacesOf(intended);

        var states = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Add(states, "LoggedIn", logged);
        Add(states, "InCombat", Flag(ConditionFlag.InCombat));
        Add(states, "InDuty", DutySolid());
        Add(states, "DutyReady", Flag(ConditionFlag.WaitingForDuty) || Flag(ConditionFlag.WaitingForDutyFinder));
        Add(states, "InQueue", Flag(ConditionFlag.InDutyQueue));
        Add(states, "Cutscene", Flag(ConditionFlag.WatchingCutscene) || Flag(ConditionFlag.WatchingCutscene78) || Flag(ConditionFlag.OccupiedInCutSceneEvent));
        Add(states, "GPose", Flag(ConditionFlag.WatchingCutscene78) && !Flag(ConditionFlag.BoundByDuty));
        Add(states, "Mounted", Flag(ConditionFlag.Mounted) || Flag(ConditionFlag.RidingPillion));
        Add(states, "Flying", Flag(ConditionFlag.InFlight));
        Add(states, "Swimming", Flag(ConditionFlag.Swimming));
        Add(states, "Diving", Flag(ConditionFlag.Diving));
        Add(states, "Crafting", Flag(ConditionFlag.Crafting) || Flag(ConditionFlag.ExecutingCraftingAction) || Flag(ConditionFlag.PreparingToCraft));
        Add(states, "Gathering", Flag(ConditionFlag.Gathering) || Flag(ConditionFlag.ExecutingGatheringAction));
        Add(states, "Dead", Flag(ConditionFlag.Unconscious));
        Add(states, "Occupied", Flag(ConditionFlag.Occupied) || Flag(ConditionFlag.Occupied30) || Flag(ConditionFlag.Occupied33) || Flag(ConditionFlag.Occupied38) || Flag(ConditionFlag.Occupied39));
        Add(states, "BetweenAreas", Flag(ConditionFlag.BetweenAreas) || Flag(ConditionFlag.BetweenAreas51));
        Add(states, "Jumping", Flag(ConditionFlag.Jumping) || Flag(ConditionFlag.Jumping61));
        Add(states, "Casting", Flag(ConditionFlag.Casting) || Flag(ConditionFlag.Casting87));
        Add(states, "Fishing", Flag(ConditionFlag.Fishing));
        Add(states, "PvP", Flag(ConditionFlag.PvPDisplayActive) || places.Contains("PvP"));
        Add(states, "Housing", Flag(ConditionFlag.UsingHousingFunctions) || places.Contains("Housing"));
        Add(states, "WeaponDrawn", player?.StatusFlags.HasFlag(StatusFlags.WeaponOut) == true);
        Add(states, "InParty", party > 0);
        Add(states, "HasTarget", Plugin.TargetManager.Target is not null);
        Add(states, "Emoting", Flag(ConditionFlag.Emoting));
        Add(states, "Performing", Flag(ConditionFlag.Performing));
        Add(states, "Trade", Flag(ConditionFlag.TradeOpen));
        Add(states, "Fashion", Flag(ConditionFlag.UsingFashionAccessory));
        Add(states, "RolePlaying", Flag(ConditionFlag.RolePlaying));
        Add(states, "Sitting", Flag(ConditionFlag.InThatPosition));
        Add(states, "Event", Flag(ConditionFlag.OccupiedInEvent) || Flag(ConditionFlag.OccupiedInQuestEvent) || Flag(ConditionFlag.OccupiedSummoningBell));
        Add(states, "DeepDungeon", Flag(ConditionFlag.InDeepDungeon) || places.Contains("DeepDungeon"));

        return new GameSnapshot
        {
            Now = now,
            LoggedIn = logged,
            TerritoryId = territoryId,
            TerritoryName = territory?.PlaceName.ValueNullable?.Name.ToString() ?? string.Empty,
            IntendedUse = intended,
            Place = PrimaryPlace(places),
            WorldName = world?.Name.ToString() ?? string.Empty,
            DataCenterName = dc?.Name.ToString() ?? string.Empty,
            JobId = job?.RowId ?? 0,
            JobAbbr = job?.Abbreviation.ToString() ?? string.Empty,
            Role = RoleOf(job?.Role ?? 0, job?.ClassJobCategory.RowId ?? 0),
            PartySize = party,
            Group = group,
            Nearby = nearby,
            Target = TargetOf(),
            LocalHour = now.Hour,
            EorzeaHour = eorzeaHour,
            Weekday = now.DayOfWeek.ToString(),
            Weather = ReadWeather(),
            States = states,
            Places = places,
        };
    }

    public string Line() =>
        LoggedIn
            ? $"{JobAbbr} · {Group} · {Place} · {WorldName} · {TerritoryName} · ET {EorzeaHour:00} · {Weather} · tgt {Target}"
            : "Not logged in";

    public bool HasPlace(string value) => Places.Contains(value);

    private static bool DutySolid() =>
        (Flag(ConditionFlag.BoundByDuty) || Flag(ConditionFlag.BoundByDuty56) || Flag(ConditionFlag.BoundByDuty95))
        && !Flag(ConditionFlag.BetweenAreas)
        && !Flag(ConditionFlag.BetweenAreas51)
        && !Flag(ConditionFlag.OccupiedInCutSceneEvent);

    private static bool Flag(ConditionFlag flag) => Plugin.Condition[flag];

    private static void Add(HashSet<string> set, string name, bool on)
    {
        if (on) set.Add(name);
    }

    private static string RoleOf(byte role, uint category)
    {
        if (category == 33) return "Crafter";
        if (category == 32) return "Gatherer";
        return role switch
        {
            1 => "Tank",
            4 => "Healer",
            2 or 3 => "DPS",
            _ => string.Empty,
        };
    }

    private static string TargetOf()
    {
        var t = Plugin.TargetManager.Target;
        if (t is null) return "none";
        return t.ObjectKind switch
        {
            ObjectKind.Pc => "player",
            ObjectKind.BattleNpc or ObjectKind.EventNpc or ObjectKind.Companion or ObjectKind.Retainer => "npc",
            _ => "any",
        };
    }

    internal static HashSet<string> PlacesOf(uint use)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        switch (use)
        {
            case 0:
                set.Add("Town");
                set.Add("Sanctuary");
                break;
            case 1:
                set.Add("Overworld");
                break;
            case 2:
                set.Add("Inn");
                set.Add("Indoor");
                set.Add("Sanctuary");
                break;
            case 3 or 4 or 7 or 57 or 58:
                set.Add("Dungeon");
                break;
            case 10:
                set.Add("Trial");
                break;
            case 8:
                set.Add("Alliance");
                break;
            case 16 or 17 or 36:
                set.Add("Raid");
                break;
            case 13:
                set.Add("Housing");
                set.Add("Sanctuary");
                break;
            case 14:
                set.Add("Housing");
                set.Add("Indoor");
                set.Add("Sanctuary");
                break;
            case 18 or 28 or 37:
                set.Add("PvP");
                break;
            case 23:
                set.Add("GoldSaucer");
                break;
            case 31:
                set.Add("DeepDungeon");
                break;
            case 41 or 47 or 48 or 60 or 61:
                set.Add("Field");
                break;
            case 49:
                set.Add("Sanctuary");
                break;
        }

        return set;
    }

    private static string PrimaryPlace(HashSet<string> places)
    {
        foreach (var key in new[] { "PvP", "DeepDungeon", "Field", "Housing", "Inn", "Town", "GoldSaucer", "Indoor", "Overworld", "Sanctuary" })
            if (places.Contains(key)) return key;
        return "Overworld";
    }

    private static string ReadWeather()
    {
        try
        {
            unsafe
            {
                var wm = WeatherManager.Instance();
                if (wm == null) return string.Empty;
                var id = wm->GetCurrentWeather();
                var row = Plugin.DataManager.GetExcelSheet<Weather>().GetRowOrDefault(id);
                return row?.Name.ToString() ?? id.ToString();
            }
        }
        catch
        {
            return string.Empty;
        }
    }
}
