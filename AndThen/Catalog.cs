using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Config;

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
        "Sitting", "Event", "DeepDungeon",
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
    public static readonly string[] Places = ["Town", "Overworld", "Indoor", "Housing", "Inn", "Sanctuary", "PvP", "GoldSaucer", "DeepDungeon", "Field"];
    public static readonly string[] Targets = ["none", "any", "player", "npc"];
    public static readonly string[] Times = ["day", "night", "dawn", "dusk"];
    public static readonly string[] Weekdays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    public static readonly string[] Statuses = ["Online", "Away", "Busy", "Roleplaying", "LookingToMeld", "LookingForParty"];
    public static readonly string[] Nearby = ["empty", "few", "crowded"];
    public static readonly string[] Compare = [">=", "<=", ">", "<", "="];
    public static readonly string[] DataCenters = ["Aether", "Primal", "Crystal", "Dynamis", "Elemental", "Gaia", "Mana", "Meteor", "Light", "Chaos", "Materia"];
    public static readonly string[] Weathers = ["Fair Skies", "Clear Skies", "Clouds", "Fog", "Wind", "Gales", "Rain", "Showers", "Thunder", "Thunderstorms", "Snow", "Blizzards", "Dust Storms", "Heat Waves", "Gloom", "Umbral Wind", "Umbral Static"];
    public static readonly string[] ValuesOnOff = ["on", "off"];
    public static readonly string[] ConfigGroups = ["Sound", "Nameplates", "Battle effects", "Graphics", "Camera", "Display", "Other"];

    public static readonly string[] ChipKinds =
    [
        "State", "Job", "Role", "Zone", "World", "Data Center", "Party size",
        "Duty", "Group size", "Time", "Weather", "Nearby", "Place", "Target",
    ];

    public static readonly string[] ThenKinds = ["Command", "Wait", "Online status", "Game setting", "Chat notice"];

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
        _ => [],
    };

    public static bool UsesList(ChipKind kind) => OptionsFor(kind).Length > 0;
    public static bool UsesCustom(ChipKind kind) =>
        kind is ChipKind.Zone or ChipKind.World or ChipKind.Weather or ChipKind.PartySize or ChipKind.Time;

    public static string HintFor(ChipKind kind) => kind switch
    {
        ChipKind.Zone => "Zone name or territory id",
        ChipKind.World => "World name",
        ChipKind.Weather => "Or type a weather name",
        ChipKind.PartySize => "Number of party members",
        ChipKind.Time => "Or type et>=18 / lt>=20",
        _ => "Value",
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

    public static string GroupOf(string option)
    {
        if (Starts(option, "Sound", "IsSnd", "IsSound")) return "Sound";
        if (Starts(option, "NamePlate", "Nameplate")) return "Nameplates";
        if (Starts(option, "BattleEffect")) return "Battle effects";
        if (Starts(option, "Fps", "FPS", "Grass", "Shadow", "Texture", "Graphics", "SSAO", "AntiAlias", "Gamma", "Lod", "Physics", "Reflection", "Parallax", "Tessellation", "Glare", "Vignet", "Distortion", "DepthOfField", "RadialBlur")) return "Graphics";
        if (Starts(option, "Camera", "Zoom", "FirstPerson", "ThirdPerson", "Lockon", "MouseOpe", "MouseAuto")) return "Camera";
        if (Starts(option, "Display", "Ui", "HUD", "Hud", "Screen")) return "Display";
        return "Other";
    }

    public static IEnumerable<string> InGroup(IEnumerable<string> names, string group) =>
        names.Where(n => GroupOf(n).Equals(group, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> Filter(IEnumerable<string> names, string query)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0) return names;
        return names.Where(n => n.Contains(query, StringComparison.OrdinalIgnoreCase) || GroupOf(n).Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool Starts(string option, params string[] prefixes) =>
        prefixes.Any(p => option.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
