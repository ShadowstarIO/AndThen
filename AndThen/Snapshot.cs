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
    public string WorldName { get; init; } = string.Empty;
    public string DataCenterName { get; init; } = string.Empty;
    public uint JobId { get; init; }
    public string JobAbbr { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int PartySize { get; init; }
    public string Group { get; init; } = "Solo";
    public int Nearby { get; init; }
    public int LocalHour { get; init; }
    public int EorzeaHour { get; init; }
    public string Weekday { get; init; } = string.Empty;
    public string Weather { get; init; } = string.Empty;
    public HashSet<string> States { get; init; } = [];

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
        var allianceZone = intended is 41 or 48 or 61;
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
        Add(states, "PvP", Flag(ConditionFlag.PvPDisplayActive));
        Add(states, "Housing", Flag(ConditionFlag.UsingHousingFunctions));
        Add(states, "WeaponDrawn", player?.StatusFlags.HasFlag(Dalamud.Game.ClientState.Objects.Enums.StatusFlags.WeaponOut) == true);
        Add(states, "InParty", party > 0);
        Add(states, "HasTarget", Plugin.TargetManager.Target is not null);
        Add(states, "Emoting", Flag(ConditionFlag.Emoting));
        Add(states, "Performing", Flag(ConditionFlag.Performing));
        Add(states, "Trade", Flag(ConditionFlag.TradeOpen));
        Add(states, "Fashion", Flag(ConditionFlag.UsingFashionAccessory));
        Add(states, "RolePlaying", Flag(ConditionFlag.RolePlaying));

        return new GameSnapshot
        {
            Now = now,
            LoggedIn = logged,
            TerritoryId = territoryId,
            TerritoryName = territory?.PlaceName.ValueNullable?.Name.ToString() ?? string.Empty,
            WorldName = world?.Name.ToString() ?? string.Empty,
            DataCenterName = dc?.Name.ToString() ?? string.Empty,
            JobId = job?.RowId ?? 0,
            JobAbbr = job?.Abbreviation.ToString() ?? string.Empty,
            Role = RoleOf(job?.Role ?? 0, job?.ClassJobCategory.RowId ?? 0),
            PartySize = party,
            Group = group,
            Nearby = nearby,
            LocalHour = now.Hour,
            EorzeaHour = eorzeaHour,
            Weekday = now.DayOfWeek.ToString(),
            Weather = ReadWeather(),
            States = states,
        };
    }

    public string Line() =>
        LoggedIn
            ? $"{JobAbbr} · {Group} · {WorldName} · {TerritoryName} · ET {EorzeaHour:00} · {Weather}"
            : "Not logged in";

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
