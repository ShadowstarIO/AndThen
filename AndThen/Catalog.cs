using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Config;
using Lumina.Excel.Sheets;

namespace AndThen;

internal static class Catalog
{
    public static readonly string[] States =
    [
        "InDuty", "DutyReady", "InQueue", "InCombat", "Cutscene", "GPose",
        "Mounted", "Flying", "Swimming", "Diving", "Crafting", "Gathering",
        "Dead", "Occupied", "BetweenAreas", "Jumping", "Casting", "Fishing",
        "PvP", "Housing", "WeaponDrawn", "InParty", "HasTarget", "Emoting",
        "Performing", "Trade", "Fashion", "RolePlaying", "LoggedIn",
        "Sitting", "Event", "DeepDungeon", "UsingItem", "Talking",
        "WatchingCutscene", "BoundByDuty",
    ];

    public static readonly string[] Jobs =
    [
        "PLD", "WAR", "DRK", "GNB", "WHM", "SCH", "AST", "SGE",
        "MNK", "DRG", "NIN", "SAM", "RPR", "VPR",
        "BRD", "MCH", "DNC", "BLM", "SMN", "RDM", "PCT", "BLU",
        "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL",
        "MIN", "BTN", "FSH",
    ];

    public static readonly string[] Roles = ["Tank", "Healer", "DPS", "Crafter", "Gatherer"];
    public static readonly string[] Duties = ["any", "none", "solid", "dungeon", "trial", "raid", "alliance", "pvp", "deep", "field"];
    public static readonly string[] Groups = ["Solo", "Light", "Full", "Alliance"];
    public static readonly string[] Places =
    [
        "Town", "Overworld", "Indoor", "Housing", "Inn", "Sanctuary",
        "PvP", "GoldSaucer", "DeepDungeon", "Field", "Dungeon", "Trial", "Raid", "Alliance",
    ];
    public static readonly string[] Targets = ["none", "any", "player", "npc"];
    public static readonly string[] Times = ["day", "night", "dawn", "dusk"];
    public static readonly string[] Weekdays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    public static readonly string[] Statuses = ["Online", "Away", "Busy", "Roleplaying", "LookingToMeld", "LookingForParty"];
    public static readonly string[] Nearby = ["empty", "few", "crowded"];
    public static readonly string[] Compare = [">=", "<=", ">", "<", "="];
    public static readonly string[] DataCenters = ["Aether", "Primal", "Crystal", "Dynamis", "Elemental", "Gaia", "Mana", "Meteor", "Light", "Chaos", "Materia"];
    public static readonly string[] Weathers =
    [
        "Fair Skies", "Clear Skies", "Clouds", "Fog", "Wind", "Gales", "Rain", "Showers",
        "Thunder", "Thunderstorms", "Snow", "Blizzards", "Dust Storms", "Heat Waves",
        "Gloom", "Umbral Wind", "Umbral Static", "Moon Dust", "Astromagnetic Storms",
    ];
    public static readonly string[] ValuesOnOff = ["on", "off"];
    public static readonly string[] Accounts = ["Home", "Visiting"];
    public static readonly string[] Mounts = ["none", "any", "flying"];
    public static readonly string[] ConfigGroups = SettingGuide.GroupNames;

    public static readonly string[] ChipKinds =
    [
        "State", "Job", "Role", "Zone", "World", "Data Center", "Party size",
        "Duty", "Group size", "Time", "Weather", "Nearby", "Place", "Target",
        "Online status", "Account", "Housing", "Mount", "Level",
    ];

    public static readonly string[] ThenKinds =
    [
        "Command", "Wait", "Online status", "Game setting", "Chat notice",
        "Log Out", "Close Game", "Penumbra Mod", "Penumbra Reset",
    ];

    public static IReadOnlyList<string> SystemOptions { get; } = Enum.GetNames<SystemConfigOption>().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    public static IReadOnlyList<string> UiOptions { get; } = Enum.GetNames<UiConfigOption>().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

    public static string[] OptionsFor(ChipKind kind) => kind switch
    {
        ChipKind.State => States,
        ChipKind.Job => Jobs,
        ChipKind.Role => Roles,
        ChipKind.Duty => Duties,
        ChipKind.Group => Groups,
        ChipKind.Nearby => Nearby,
        ChipKind.Place => Places,
        ChipKind.Target => Targets,
        ChipKind.Time => Times.Concat(Weekdays).ToArray(),
        ChipKind.Weather => Weathers,
        ChipKind.DataCenter => DataCenters,
        ChipKind.OnlineStatus => Statuses,
        ChipKind.Account => Accounts,
        ChipKind.Mount => Mounts,
        ChipKind.Zone => ZoneNames(),
        ChipKind.World => WorldNames(),
        _ => [],
    };

    public static bool UsesList(ChipKind kind) => OptionsFor(kind).Length > 0;
    public static bool UsesCustom(ChipKind kind) =>
        kind is ChipKind.Zone or ChipKind.World or ChipKind.Weather or ChipKind.PartySize or ChipKind.Time or ChipKind.Nearby or ChipKind.Mount or ChipKind.Level;
    public static bool LongList(ChipKind kind) =>
        kind is ChipKind.Zone or ChipKind.World or ChipKind.Weather or ChipKind.State or ChipKind.Job;

    public static string HintFor(ChipKind kind) => kind switch
    {
        ChipKind.Zone => "Zone name or territory id",
        ChipKind.World => "World name",
        ChipKind.Weather => "Or type a weather name",
        ChipKind.PartySize => "Number of party members",
        ChipKind.Time => "Or type et>=18 / lt>=20",
        ChipKind.Nearby => "empty / few / crowded or >=3",
        ChipKind.Mount => "none, any, flying, or a mount name",
        ChipKind.Level => "Or type >=90",
        ChipKind.Housing => "District, ward, plot, subdivision",
        _ => "Value",
    };

    public static string CurrentFor(ChipKind kind, GameSnapshot snap) => kind switch
    {
        ChipKind.State => snap.States.Contains("LoggedIn") ? "LoggedIn" : string.Empty,
        ChipKind.Job => snap.JobAbbr,
        ChipKind.Role => snap.Role,
        ChipKind.Zone => snap.TerritoryName,
        ChipKind.World => snap.WorldName,
        ChipKind.DataCenter => snap.DataCenterName,
        ChipKind.PartySize => snap.PartySize.ToString(),
        ChipKind.Duty => snap.States.Contains("InDuty") ? "solid" : "none",
        ChipKind.Group => snap.Group,
        ChipKind.Time => $"et={snap.EorzeaHour}",
        ChipKind.Weather => snap.Weather,
        ChipKind.Nearby => snap.Nearby.ToString(),
        ChipKind.Place => snap.Place,
        ChipKind.Target => snap.Target,
        ChipKind.OnlineStatus => snap.OnlineStatus,
        ChipKind.Account => snap.Account,
        ChipKind.Housing => Housing.Encode(snap.Address),
        ChipKind.Mount => snap.States.Contains("Flying") ? "flying" : snap.States.Contains("Mounted") ? "any" : "none",
        ChipKind.Level => snap.Level.ToString(),
        _ => string.Empty,
    };

    public static string DutyLabel(string value) => value.ToLowerInvariant() switch
    {
        "any" => "Any duty activity",
        "none" => "Not in a duty",
        "solid" => "Inside an instance",
        "dungeon" => "Dungeon",
        "trial" => "Trial",
        "raid" => "Raid",
        "alliance" => "Alliance raid / field",
        "pvp" => "PvP",
        "deep" => "Deep dungeon",
        "field" => "Field operation",
        _ => value,
    };

    public static string GroupOf(string option) => SettingGuide.GroupOf(option);

    public static IEnumerable<string> InGroup(IEnumerable<string> names, string group) =>
        names.Where(n => GroupOf(n).Equals(group, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> Filter(IEnumerable<string> names, string query)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0) return names;
        return names.Where(n => n.Contains(query, StringComparison.OrdinalIgnoreCase) || SettingGuide.SearchBlob(n).Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    public static string CurrentSetting(ConfigSection section, string option)
    {
        option = (option ?? string.Empty).Trim();
        if (option.Length == 0) return string.Empty;
        try
        {
            if (section == ConfigSection.Ui)
            {
                if (!Enum.TryParse<UiConfigOption>(option, true, out var ui)) return string.Empty;
                if (Plugin.GameConfig.TryGet(ui, out uint n)) return n.ToString();
                if (Plugin.GameConfig.TryGet(ui, out float f)) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (Plugin.GameConfig.TryGet(ui, out bool on)) return on ? "1" : "0";
                return string.Empty;
            }
            if (!Enum.TryParse<SystemConfigOption>(option, true, out var sys)) return string.Empty;
            if (Plugin.GameConfig.TryGet(sys, out uint sn)) return sn.ToString();
            if (Plugin.GameConfig.TryGet(sys, out float sf)) return sf.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (Plugin.GameConfig.TryGet(sys, out bool son)) return son ? "1" : "0";
        }
        catch { /* config not ready */ }
        return string.Empty;
    }

    public static string[] ZoneNames()
    {
        try
        {
            var sheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheet)
            {
                var name = row.PlaceName.ValueNullable?.Name.ToString();
                if (!string.IsNullOrWhiteSpace(name)) set.Add(name);
            }
            return set.ToArray();
        }
        catch { return []; }
    }

    public static string[] WorldNames()
    {
        try
        {
            var sheet = Plugin.DataManager.GetExcelSheet<World>();
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheet)
            {
                if (row.DataCenter.RowId == 0) continue;
                var name = row.Name.ToString();
                if (!string.IsNullOrWhiteSpace(name)) set.Add(name);
            }
            return set.ToArray();
        }
        catch { return []; }
    }
}
