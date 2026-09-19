using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
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

        var states = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Add(states, "InCombat", Flag(ConditionFlag.InCombat));
        Add(states, "InDuty", Flag(ConditionFlag.BoundByDuty) || Flag(ConditionFlag.BoundByDuty56));
        Add(states, "Cutscene", Flag(ConditionFlag.WatchingCutscene) || Flag(ConditionFlag.OccupiedInCutSceneEvent));
        Add(states, "Mounted", Flag(ConditionFlag.Mounted));
        Add(states, "Flying", Flag(ConditionFlag.InFlight));
        Add(states, "Swimming", Flag(ConditionFlag.Swimming));
        Add(states, "Diving", Flag(ConditionFlag.Diving));
        Add(states, "Crafting", Flag(ConditionFlag.Crafting) || Flag(ConditionFlag.Crafting40));
        Add(states, "Gathering", Flag(ConditionFlag.Gathering) || Flag(ConditionFlag.Gathering42));
        Add(states, "Dead", Flag(ConditionFlag.Unconscious));
        Add(states, "Occupied", Flag(ConditionFlag.Occupied) || Flag(ConditionFlag.Occupied30) || Flag(ConditionFlag.Occupied33) || Flag(ConditionFlag.Occupied38) || Flag(ConditionFlag.Occupied39));
        Add(states, "BetweenAreas", Flag(ConditionFlag.BetweenAreas) || Flag(ConditionFlag.BetweenAreas51));
        Add(states, "Jumping", Flag(ConditionFlag.Jumping) || Flag(ConditionFlag.Jumping61));
        Add(states, "Casting", Flag(ConditionFlag.Casting));
        Add(states, "Fishing", Flag(ConditionFlag.Fishing));
        Add(states, "PvP", Flag(ConditionFlag.PvPDisplayActive));
        Add(states, "UsingHousing", Flag(ConditionFlag.UsingHousingFunctions));
        Add(states, "WeaponDrawn", player?.StatusFlags.HasFlag(Dalamud.Game.ClientState.Objects.Enums.StatusFlags.WeaponOut) == true);
        Add(states, "InParty", Plugin.PartyList.Length > 0);

        var role = RoleOf(job?.Role ?? 0, job?.ClassJobCategory.RowId ?? 0);
        return new GameSnapshot
        {
            Now = DateTime.Now,
            LoggedIn = logged,
            TerritoryId = territoryId,
            TerritoryName = territory?.PlaceName.ValueNullable?.Name.ToString() ?? string.Empty,
            WorldName = world?.Name.ToString() ?? string.Empty,
            DataCenterName = dc?.Name.ToString() ?? string.Empty,
            JobId = job?.RowId ?? 0,
            JobAbbr = job?.Abbreviation.ToString() ?? string.Empty,
            Role = role,
            PartySize = Plugin.PartyList.Length,
            States = states,
        };
    }

    public string Line() =>
        LoggedIn
            ? $"{JobAbbr} · {WorldName} · {TerritoryName} ({TerritoryId}) · party {PartySize}"
            : "Not logged in";

    private static bool Flag(ConditionFlag flag) => Plugin.Condition[flag];

    private static void Add(HashSet<string> set, string name, bool on)
    {
        if (on) set.Add(name);
    }

    private static string RoleOf(byte role, uint category) =>
        category is 33 => "Crafter",
        category is 32 => "Gatherer",
        role is 1 => "Tank",
        role is 4 => "Healer",
        role is 2 or 3 => "DPS",
        _ => string.Empty;
}
