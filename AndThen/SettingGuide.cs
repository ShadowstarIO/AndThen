using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AndThen;

internal enum SettingKind
{
    Toggle,
    Volume,
    Choice,
    Number,
    Float,
}

internal sealed class SettingInfo
{
    public string Option { get; init; } = string.Empty;
    public ConfigSection Section { get; init; }
    public string Group { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Menu { get; init; } = string.Empty;
    public string About { get; init; } = string.Empty;
    public SettingKind Kind { get; init; }
    public int Min { get; init; }
    public int Max { get; init; }
    public (string Value, string Label)[] Choices { get; init; } = [];
}

internal static class SettingGuide
{
    public static readonly string[] GroupNames =
    [
        "Display", "Sound", "Graphics", "Battle effects", "Nameplates", "Cutscenes", "Camera", "All settings",
    ];

    private static readonly (string Value, string Label)[] ScreenModes =
    [
        ("0", "Windowed"),
        ("1", "Borderless Windowed"),
        ("2", "Full Screen"),
    ];

    private static readonly (string Value, string Label)[] FrameRates =
    [
        ("0", "None"),
        ("1", "Main display refresh rate"),
        ("2", "60 fps"),
        ("3", "30 fps"),
    ];

    private static readonly (string Value, string Label)[] BattleEffects =
    [
        ("0", "Show All"),
        ("1", "Show Limited"),
        ("2", "Show None"),
    ];

    private static readonly (string Value, string Label)[] NameDisp =
    [
        ("1", "Always"),
        ("2", "During Battle"),
        ("5", "Out of Combat"),
        ("3", "When Targeted"),
        ("4", "Never"),
    ];

    private static readonly (string Value, string Label)[] NameType =
    [
        ("1", "Full Name"),
        ("2", "Surname Abbreviated"),
        ("3", "Forename Abbreviated"),
        ("4", "Initials"),
    ];

    private static readonly (string Value, string Label)[] ShadowCast =
    [
        ("0", "Display"),
        ("1", "Hide"),
    ];

    private static readonly SettingInfo[] Entries = Build();
    private static readonly Dictionary<string, SettingInfo> ByOption = Index();

    public static IEnumerable<SettingInfo> InGroup(string group) =>
        Entries.Where(e => e.Group.Equals(group, StringComparison.OrdinalIgnoreCase));

    public static SettingInfo? Find(string option)
    {
        option = (option ?? string.Empty).Trim();
        if (option.Length == 0) return null;
        if (ByOption.TryGetValue(option, out var info)) return info;
        if (option.EndsWith("_DX11", StringComparison.Ordinal) && ByOption.TryGetValue(option[..^5], out info)) return info;
        if (ByOption.TryGetValue(option + "_DX11", out info)) return info;
        return null;
    }

    public static string GroupOf(string option) => Find(option)?.Group ?? string.Empty;

    public static string SearchBlob(string option)
    {
        var info = Find(option);
        return info is null ? option : info.Label + " " + info.Menu + " " + info.About + " " + info.Option;
    }

    public static string Display(string option, string raw)
    {
        raw = (raw ?? string.Empty).Trim();
        var info = Find(option);
        if (info is null || raw.Length == 0) return raw;
        foreach (var choice in info.Choices)
            if (choice.Value == raw) return choice.Label;
        if (info.Kind == SettingKind.Toggle)
        {
            if (raw is "1" or "on" or "true") return "On";
            if (raw is "0" or "off" or "false") return "Off";
        }
        return raw;
    }

    public static string RowLabel(string option, string raw)
    {
        if (string.IsNullOrWhiteSpace(option)) return "Game Setting";
        var info = Find(option);
        var name = info?.Label ?? option;
        var shown = Display(option, raw);
        return string.IsNullOrWhiteSpace(shown) ? name : $"{name} = {shown}";
    }

    public static bool TryCoerce(string option, string raw, out uint number, out float floating, out bool isFloat)
    {
        number = 0;
        floating = 0;
        isFloat = false;
        raw = (raw ?? string.Empty).Trim();
        var info = Find(option);
        if (info?.Kind == SettingKind.Float || FloatOptions.Contains(option))
        {
            isFloat = true;
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out floating)
                || float.TryParse(raw, out floating);
        }

        if (info is not null)
        {
            foreach (var choice in info.Choices)
            {
                if (raw.Equals(choice.Value, StringComparison.OrdinalIgnoreCase)
                    || raw.Equals(choice.Label, StringComparison.OrdinalIgnoreCase))
                    return uint.TryParse(choice.Value, out number);
            }

            if (Alias(option, raw, out number)) return true;
            if (info.Kind == SettingKind.Toggle && OnOff(raw, out number)) return true;
            return uint.TryParse(raw, out number);
        }

        if (uint.TryParse(raw, out number)) return true;
        if (Alias(option, raw, out number)) return true;
        return OnOff(raw, out number);
    }

    private static bool Alias(string option, string raw, out uint number)
    {
        number = 0;
        var key = raw.Trim().ToLowerInvariant();
        if (option.Contains("BattleEffect", StringComparison.OrdinalIgnoreCase))
        {
            number = key switch
            {
                "all" or "show all" => 0,
                "simple" or "limited" or "show limited" => 1,
                "none" or "show none" => 2,
                "off" => 2,
                _ => uint.MaxValue,
            };
            return number != uint.MaxValue;
        }

        if (option.StartsWith("NamePlateDispType", StringComparison.OrdinalIgnoreCase))
        {
            number = key switch
            {
                "always" => 1,
                "during battle" or "battle" or "combat" => 2,
                "out of combat" or "not in combat" => 5,
                "when targeted" or "targeted" or "target" => 3,
                "never" => 4,
                _ => uint.MaxValue,
            };
            return number != uint.MaxValue;
        }

        if (option.StartsWith("NamePlateNameType", StringComparison.OrdinalIgnoreCase))
        {
            number = key switch
            {
                "full" or "full name" => 1,
                "surname" or "surname abbreviated" => 2,
                "forename" or "forename abbreviated" => 3,
                "initials" => 4,
                _ => uint.MaxValue,
            };
            return number != uint.MaxValue;
        }

        if (option.StartsWith("ScreenMode", StringComparison.OrdinalIgnoreCase))
        {
            number = key switch
            {
                "windowed" => 0,
                "borderless" or "borderless windowed" => 1,
                "full" or "fullscreen" or "full screen" => 2,
                _ => uint.MaxValue,
            };
            return number != uint.MaxValue;
        }

        return false;
    }

    private static bool OnOff(string raw, out uint number)
    {
        if (raw.Equals("on", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            number = 1;
            return true;
        }

        if (raw.Equals("off", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("false", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            number = 0;
            return true;
        }

        number = 0;
        return false;
    }

    private static readonly HashSet<string> FloatOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "MouseSpeed", "DeadArea",
        "FirstPersonDefaultYAngle", "FirstPersonDefaultZoom", "FirstPersonDefaultDistance",
        "ThirdPersonDefaultYAngle", "ThirdPersonDefaultZoom", "ThirdPersonDefaultDistance",
        "LockonDefaultYAngle", "LockonDefaultZoom",
    };

    private static Dictionary<string, SettingInfo> Index()
    {
        var map = new Dictionary<string, SettingInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Entries)
            map[entry.Option] = entry;
        return map;
    }

    private static SettingInfo[] Build()
    {
        var list = new List<SettingInfo>();
        const string display = "System Configuration > Display Settings";
        const string sound = "System Configuration > Sound Settings";
        const string graphics = "System Configuration > Graphics Settings";
        const string character = "Character Configuration > Control Settings > Character";
        const string names = "Character Configuration > Display Name Settings";
        const string cuts = "Character Configuration > Control Settings";
        const string camera = "System Configuration > Mouse Settings";

        list.Add(Choice("ScreenMode", ConfigSection.System, "Display", "Screen Mode", display,
            "How the client fills the monitor. 0 Windowed, 1 Borderless Windowed, 2 Full Screen.", ScreenModes));
        list.Add(Num("ScreenWidth", ConfigSection.System, "Display", "Window Width", display, "Width of the window, in pixels, while Windowed."));
        list.Add(Num("ScreenHeight", ConfigSection.System, "Display", "Window Height", display, "Height of the window, in pixels, while Windowed."));
        list.Add(Num("FullScreenWidth", ConfigSection.System, "Display", "Full Screen Width", display, "Width used by Full Screen mode."));
        list.Add(Num("FullScreenHeight", ConfigSection.System, "Display", "Full Screen Height", display, "Height used by Full Screen mode."));
        list.Add(Num("Refreshrate", ConfigSection.System, "Display", "Refresh Rate", display, "Refresh rate used by Full Screen mode."));
        list.Add(Choice("Fps", ConfigSection.System, "Display", "Frame Rate", display,
            "Frame rate cap. 0 None, 1 the main display's refresh rate, 2 is 60 fps, 3 is 30 fps.", FrameRates));
        list.Add(Toggle("FPSInActive", ConfigSection.System, "Display", "Limit Frame Rate When Inactive", display,
            "Caps the frame rate while the client window is in the background."));
        list.Add(Toggle("FPSDownAFK", ConfigSection.System, "Display", "Limit Frame Rate When Away", display,
            "Caps the frame rate while you are away from the keyboard."));
        list.Add(Vol("Gamma", ConfigSection.System, "Display", "Gamma", display, "Full screen gamma. 0 to 100. The slider in Display Settings is this value.", 0, 100));
        list.Add(Num("UiBaseScale", ConfigSection.System, "Display", "UI Size", display, "Default HUD and window size. Larger numbers are bigger. The client may ask you to restart."));
        list.Add(Toggle("UiHighScale", ConfigSection.System, "Display", "High Resolution UI", display, "UI scale used on a high resolution display. The client may ask you to restart."));
        list.Add(Num("UiAssetType", ConfigSection.System, "Display", "UI Resolution", display, "Which UI asset set is loaded. Changing it asks for a restart. Use the current value after picking it in Display Settings."));
        list.Add(Num("CharaLight", ConfigSection.System, "Display", "Character Lighting", display, "Character lighting preset from Display Settings. Use the current value after picking it."));
        list.Add(Vol("GraphicsRezoScale", ConfigSection.System, "Display", "3D Resolution Scaling", display, "Internal render scale. The slider runs from 50 to 100.", 50, 100));
        list.Add(Num("GraphicsRezoUpscaleType", ConfigSection.System, "Display", "Graphics Upscaling", display, "Upscaler from Display Settings, such as none, FSR, or DLSS. Use the current value so the index matches the menu."));
        list.Add(Toggle("DynamicRezoType", ConfigSection.System, "Display", "Dynamic Resolution", display, "Lets the client lower the internal resolution when the frame rate drops."));
        list.Add(Num("DynamicRezoThreshold", ConfigSection.System, "Display", "Dynamic Resolution Threshold", display, "Frame rate at which dynamic resolution starts. Use the current value from Display Settings."));
        list.Add(Toggle("DynamicRezoEnableCutScene", ConfigSection.System, "Display", "Dynamic Resolution in Cutscenes", display, "Allows dynamic resolution while a cutscene is playing."));

        Vols(list, sound, "Master Volume", "SoundMaster", "IsSndMaster", "Overall volume, and the checkbox that enables it.");
        Vols(list, sound, "BGM", "SoundBgm", "IsSndBgm", "Background music volume, and the checkbox that enables it.");
        Vols(list, sound, "Sound Effects", "SoundSe", "IsSndSe", "Sound effect volume, and the checkbox that enables it.");
        Vols(list, sound, "Voice", "SoundVoice", "IsSndVoice", "Voice volume, and the checkbox that enables it.");
        Vols(list, sound, "Ambient", "SoundEnv", "IsSndEnv", "Ambient environment volume, and the checkbox that enables it.");
        Vols(list, sound, "System Sounds", "SoundSystem", "IsSndSystem", "System sound volume, and the checkbox that enables it.");
        Vols(list, sound, "Performance", "SoundPerform", "IsSndPerform", "Performance mode volume, and the checkbox that enables it.");
        list.Add(Vol("SoundPlayer", ConfigSection.System, "Sound", "Player Effects", sound, "Volume of your own battle effects. 0 to 100.", 0, 100));
        list.Add(Vol("SoundParty", ConfigSection.System, "Sound", "Party Effects", sound, "Volume of party member effects. 0 to 100.", 0, 100));
        list.Add(Vol("SoundOther", ConfigSection.System, "Sound", "Other Effects", sound, "Volume of other players' effects. 0 to 100.", 0, 100));
        list.Add(Vol("SoundChocobo", ConfigSection.System, "Sound", "Chocobo Music", sound, "Chocobo music volume. 0 to 100.", 0, 100));
        list.Add(Vol("SoundHousing", ConfigSection.System, "Sound", "Housing Music", sound, "Volume used in residential areas. 0 to 100.", 0, 100));
        list.Add(Vol("SoundFieldBattle", ConfigSection.System, "Sound", "Field Battle Music", sound, "Normal battle music volume. 0 to 100.", 0, 100));
        list.Add(Vol("SoundCfTimeCount", ConfigSection.System, "Sound", "Duty Finder Countdown", sound, "System sounds played while waiting on the Duty Finder. 0 to 100.", 0, 100));
        list.Add(Toggle("IsSoundAlways", ConfigSection.System, "Sound", "Play While Inactive", sound, "Keeps sound playing while the window is in the background."));
        list.Add(Toggle("IsSoundBgmAlways", ConfigSection.System, "Sound", "BGM While Inactive", sound, "Keeps background music playing while the window is inactive."));
        list.Add(Toggle("IsSoundSeAlways", ConfigSection.System, "Sound", "Effects While Inactive", sound, "Keeps sound effects playing while the window is inactive."));
        list.Add(Toggle("IsSoundVoiceAlways", ConfigSection.System, "Sound", "Voice While Inactive", sound, "Keeps voices playing while the window is inactive."));
        list.Add(Toggle("IsSoundEnvAlways", ConfigSection.System, "Sound", "Ambient While Inactive", sound, "Keeps ambient sound playing while the window is inactive."));
        list.Add(Toggle("IsSoundSystemAlways", ConfigSection.System, "Sound", "System Sounds While Inactive", sound, "Keeps system sounds playing while the window is inactive."));
        list.Add(Toggle("IsSoundPerformAlways", ConfigSection.System, "Sound", "Performance While Inactive", sound, "Keeps performance audio playing while the window is inactive."));
        list.Add(Toggle("Is3DAudio", ConfigSection.System, "Sound", "3D Audio", sound, "Positional audio from the Sound Settings tab."));
        list.Add(Toggle("SoundDolby", ConfigSection.System, "Sound", "Dolby Atmos", sound, "Dolby Atmos for headphones, from the Sound Settings tab."));
        list.Add(Num("SoundEqualizerType", ConfigSection.System, "Sound", "Equalizer", sound, "Equalizer preset index from the Sound Settings tab. Use the current value after choosing the preset."));
        list.Add(Toggle("IsSoundDisable", ConfigSection.System, "Sound", "Mute", sound, "Master mute stored by the client. On means sound is disabled."));

        list.Add(Toggle("OcclusionCulling_DX11", ConfigSection.System, "Graphics", "Occlusion Culling", graphics + " > General",
            "Disable rendering of objects when they are not visible. Also writes the non-DX11 value."));
        list.Add(Toggle("WaterWet_DX11", ConfigSection.System, "Graphics", "Wet Surface Effects", graphics + " > General",
            "Enable wet surface effects. Also writes the non-DX11 value."));
        list.Add(Toggle("LodType_DX11", ConfigSection.System, "Graphics", "Distant Object LOD", graphics + " > General",
            "Use low-detail models on distant objects. Also writes the non-DX11 value."));
        list.Add(Toggle("GrassEnableDynamicInterference", ConfigSection.System, "Graphics", "Dynamic Grass", graphics + " > General",
            "Enable dynamic grass interaction."));
        list.Add(Num("ReflectionType_DX11", ConfigSection.System, "Graphics", "Real-time Reflections", graphics + " > General",
            "Reflection quality. The menu uses steps such as Off and Standard. Use the current value after setting it. Also writes the non-DX11 value."));
        list.Add(Num("AntiAliasing_DX11", ConfigSection.System, "Graphics", "Edge Smoothing", graphics + " > General",
            "Anti-aliasing. The menu lists modes such as FXAA and TSCMAA. Use the current value so the index matches. Also writes the non-DX11 value."));
        list.Add(Num("TranslucentQuality_DX11", ConfigSection.System, "Graphics", "Transparent Lighting Quality", graphics + " > General",
            "How light hits transparent effects such as spells and glass. Use the current value. Also writes the non-DX11 value."));
        list.Add(Num("GrassQuality_DX11", ConfigSection.System, "Graphics", "Grass Quality", graphics + " > General",
            "Density of grass. The menu uses steps such as Off, Normal, and High. Also writes the non-DX11 value."));
        list.Add(Num("ParallaxOcclusion_DX11", ConfigSection.System, "Graphics", "Parallax Occlusion", graphics + " > General",
            "Depth on flat textures such as brick and stone. Quality steps start at Off."));
        list.Add(Num("Tessellation_DX11", ConfigSection.System, "Graphics", "Tessellation", graphics + " > General",
            "Extra geometric detail on terrain. Quality steps start at Off."));
        list.Add(Num("GlareRepresentation_DX11", ConfigSection.System, "Graphics", "Glare Quality", graphics + " > General",
            "Glare quality from the General tab. Use the current value after choosing it."));

        list.Add(Choice("ShadowVisibilityTypeSelf_DX11", ConfigSection.System, "Graphics", "Shadows: Self", graphics + " > Shadows",
            "Show or hide your shadow. 0 Display, 1 Hide. Also writes the non-DX11 value.", ShadowCast));
        list.Add(Choice("ShadowVisibilityTypeParty_DX11", ConfigSection.System, "Graphics", "Shadows: Party", graphics + " > Shadows",
            "Show or hide party member shadows. 0 Display, 1 Hide. Also writes the non-DX11 value.", ShadowCast));
        list.Add(Choice("ShadowVisibilityTypeOther_DX11", ConfigSection.System, "Graphics", "Shadows: Other", graphics + " > Shadows",
            "Show or hide shadows for other players and NPCs. 0 Display, 1 Hide. Also writes the non-DX11 value.", ShadowCast));
        list.Add(Choice("ShadowVisibilityTypeEnemy_DX11", ConfigSection.System, "Graphics", "Shadows: Enemies", graphics + " > Shadows",
            "Show or hide enemy shadows. 0 Display, 1 Hide. Also writes the non-DX11 value.", ShadowCast));
        list.Add(Toggle("ShadowLOD_DX11", ConfigSection.System, "Graphics", "Shadow LOD", graphics + " > Shadow Quality",
            "Use low-detail models on shadows. Also writes the non-DX11 value."));
        list.Add(Toggle("ShadowBgLOD", ConfigSection.System, "Graphics", "Distant Shadow LOD", graphics + " > Shadow Quality",
            "Use low-detail models on shadows of distant objects."));
        list.Add(Num("ShadowTextureSizeType_DX11", ConfigSection.System, "Graphics", "Shadow Resolution", graphics + " > Shadow Quality",
            "How sharp shadows are. Higher steps are sharper. Also writes the non-DX11 value."));
        list.Add(Num("ShadowCascadeCountType_DX11", ConfigSection.System, "Graphics", "Shadow Cascading", graphics + " > Shadow Quality",
            "Shadow quality over distance. Also writes the non-DX11 value."));
        list.Add(Num("ShadowSoftShadowType_DX11", ConfigSection.System, "Graphics", "Shadow Softening", graphics + " > Shadow Quality",
            "How soft shadow edges are. The menu includes a strongest step. Also writes the non-DX11 value."));
        list.Add(Num("ShadowLightValidType", ConfigSection.System, "Graphics", "Cast Shadows", graphics + " > Shadow Quality",
            "Which characters and objects cast shadows, from Minimum toward everyone. Use the current value."));
        list.Add(Num("TextureRezoType", ConfigSection.System, "Graphics", "Texture Resolution", graphics + " > Texture Detail",
            "Texture detail. Changing it asks for a restart. Use the current value."));
        list.Add(Num("TextureFilterQuality_DX11", ConfigSection.System, "Graphics", "Texture Filtering", graphics + " > Texture Detail",
            "Texture filter quality. Also writes the non-DX11 value."));
        list.Add(Num("TextureAnisotropicQuality_DX11", ConfigSection.System, "Graphics", "Anisotropic Filtering", graphics + " > Texture Detail",
            "Keeps textures sharp at an angle. Also writes the non-DX11 value."));
        list.Add(Num("PhysicsTypeSelf_DX11", ConfigSection.System, "Graphics", "Movement Physics: Self", graphics + " > Movement Physics",
            "Cloth and hair physics for you. Use the current value from the menu. Also writes the non-DX11 value."));
        list.Add(Num("PhysicsTypeParty_DX11", ConfigSection.System, "Graphics", "Movement Physics: Party", graphics + " > Movement Physics",
            "Cloth and hair physics for party members. Also writes the non-DX11 value."));
        list.Add(Num("PhysicsTypeOther_DX11", ConfigSection.System, "Graphics", "Movement Physics: Other", graphics + " > Movement Physics",
            "Cloth and hair physics for other characters. Also writes the non-DX11 value."));
        list.Add(Num("PhysicsTypeEnemy_DX11", ConfigSection.System, "Graphics", "Movement Physics: Enemies", graphics + " > Movement Physics",
            "Cloth and hair physics for enemies. Also writes the non-DX11 value."));
        list.Add(Toggle("Vignetting_DX11", ConfigSection.System, "Graphics", "Limb Darkening", graphics + " > Effects",
            "Naturally darken the edges of the screen. Also writes the non-DX11 value."));
        list.Add(Toggle("RadialBlur_DX11", ConfigSection.System, "Graphics", "Radial Blur", graphics + " > Effects",
            "Blur around an object in motion. Also writes the non-DX11 value."));
        list.Add(Num("SSAO_DX11", ConfigSection.System, "Graphics", "Ambient Occlusion", graphics + " > Effects",
            "Screen space ambient occlusion. The menu lists Off, GTAO, and HBAO+. Use the current value. Also writes the non-DX11 value."));
        list.Add(Num("Glare_DX11", ConfigSection.System, "Graphics", "Glare", graphics + " > Effects",
            "Bloom strength. Usual steps are Off, Standard, and High. Also writes the non-DX11 value."));
        list.Add(Num("DistortionWater_DX11", ConfigSection.System, "Graphics", "Water Reflection", graphics + " > Effects",
            "Water reflection quality. Also writes the non-DX11 value."));
        list.Add(Toggle("DepthOfField_DX11", ConfigSection.System, "Graphics", "Depth of Field", graphics + " > Cinematic Cutscenes",
            "Enable depth of field during cinematic cutscenes. Also writes the non-DX11 value."));
        list.Add(Num("DisplayObjectLimitType2", ConfigSection.System, "Graphics", "Character and Object Quantity", graphics + " > Display Limits",
            "How many characters and objects are drawn. Use the current value from Display Limits."));
        list.Add(Num("DynamicAroundRangeMode", ConfigSection.System, "Graphics", "Character Display Range", graphics + " > Display Limits",
            "How far away characters stay visible. Use the current value."));
        list.Add(Num("DisplayHouseIndoorPcLimitType", ConfigSection.System, "Graphics", "Players Shown Indoors", graphics + " > Display Limits",
            "How many players are drawn inside a house."));
        list.Add(Num("MapResolution_DX11", ConfigSection.System, "Graphics", "Map Resolution", graphics + " > General",
            "Resolution of the area map. Also writes the non-DX11 value."));

        list.Add(Choice("BattleEffectSelf", ConfigSection.Ui, "Battle effects", "Battle Effects: Self", character,
            "Your battle effects. Same list as /battleeffect self: Show All, Show Limited, Show None.", BattleEffects));
        list.Add(Choice("BattleEffectParty", ConfigSection.Ui, "Battle effects", "Battle Effects: Party", character,
            "Party member battle effects. /battleeffect party.", BattleEffects));
        list.Add(Choice("BattleEffectOther", ConfigSection.Ui, "Battle effects", "Battle Effects: Others", character,
            "Other NPC battle effects. /battleeffect other.", BattleEffects));
        list.Add(Choice("BattleEffectPvPEnemyPc", ConfigSection.Ui, "Battle effects", "Battle Effects: PvP Opponents", character,
            "Enemy player battle effects in PvP. /battleeffect enemypc.", BattleEffects));

        AddDisp(list, "NamePlateDispTypeSelf", "Nameplate: You");
        AddDisp(list, "NamePlateDispTypeParty", "Nameplate: Party");
        AddDisp(list, "NamePlateDispTypeAlliance", "Nameplate: Alliance");
        AddDisp(list, "NamePlateDispTypeOther", "Nameplate: Other Players");
        AddDisp(list, "NamePlateDispTypeFriend", "Nameplate: Friends");
        AddDisp(list, "NamePlateDispTypeFeast", "Nameplate: Crystalline Conflict");
        AddDisp(list, "NamePlateDispTypeSelfPet", "Nameplate: Your Pet");
        AddDisp(list, "NamePlateDispTypeSelfBuddy", "Nameplate: Your Companion");
        AddDisp(list, "NamePlateDispTypePartyPet", "Nameplate: Party Pets");
        AddDisp(list, "NamePlateDispTypePartyBuddy", "Nameplate: Party Companions");
        AddDisp(list, "NamePlateDispTypeOtherPet", "Nameplate: Other Pets");
        AddDisp(list, "NamePlateDispTypeOtherBuddy", "Nameplate: Other Companions");
        AddDisp(list, "NamePlateDispTypeNpc", "Nameplate: NPCs");
        AddDisp(list, "NamePlateDispTypeMinion", "Nameplate: Minions");
        AddDisp(list, "NamePlateDispTypeEngagedEnemy", "Nameplate: Engaged Enemies");
        AddDisp(list, "NamePlateDispTypeUnengagedEnemy", "Nameplate: Unengaged Enemies");
        AddName(list, "NamePlateNameTypeSelf", "Name Style: You");
        AddName(list, "NamePlateNameTypeParty", "Name Style: Party");
        AddName(list, "NamePlateNameTypeAlliance", "Name Style: Alliance");
        AddName(list, "NamePlateNameTypeOther", "Name Style: Other Players");
        AddName(list, "NamePlateNameTypeFriend", "Name Style: Friends");
        AddName(list, "NamePlateNameTypeFeast", "Name Style: Crystalline Conflict");
        list.Add(Toggle("NamePlateDispJobIcon", ConfigSection.Ui, "Nameplates", "Job Icons", names, "Show job icons on nameplates."));
        list.Add(Toggle("NamePlateSetRoleColor", ConfigSection.Ui, "Nameplates", "Role Colors", names, "Color nameplates by role."));
        list.Add(Toggle("NamePlateDispWorldTravel", ConfigSection.Ui, "Nameplates", "Traveler World", names, "Show when a player is visiting from another world."));
        list.Add(Num("NamePlateDispSize", ConfigSection.Ui, "Nameplates", "Nameplate Size", names, "Nameplate size step from Display Name Settings. Use the current value."));
        list.Add(Num("NamePlateHpTypeSelf", ConfigSection.Ui, "Nameplates", "HP Bar: You", names, "When your HP bar is shown. Set it in Display Name Settings, then use the current value."));
        list.Add(Num("NamePlateHpTypeParty", ConfigSection.Ui, "Nameplates", "HP Bar: Party", names, "When party HP bars are shown. Use the current value."));
        list.Add(Num("NamePlateHpTypeAlliance", ConfigSection.Ui, "Nameplates", "HP Bar: Alliance", names, "When alliance HP bars are shown. Use the current value."));
        list.Add(Num("NamePlateHpTypeOther", ConfigSection.Ui, "Nameplates", "HP Bar: Others", names, "When other players' HP bars are shown. Use the current value."));

        list.Add(Toggle("CutsceneSkipIsShip", ConfigSection.Ui, "Cutscenes", "Skip Viewed Story Cutscenes", cuts,
            "Skip story cutscenes you have already seen."));
        list.Add(Toggle("CutsceneSkipIsContents", ConfigSection.Ui, "Cutscenes", "Skip Viewed Content Cutscenes", cuts,
            "Skip duty and other content cutscenes you have already seen."));
        list.Add(Toggle("CutsceneSkipIsHousing", ConfigSection.Ui, "Cutscenes", "Skip Viewed Housing Cutscenes", cuts,
            "Skip housing cutscenes you have already seen."));
        list.Add(Num("CutsceneMovieVoice", ConfigSection.System, "Cutscenes", "Movie Voice", "System Configuration > Sound Settings",
            "Voice language used for movies. Use the current value after choosing it."));
        list.Add(Num("CutsceneMovieCaption", ConfigSection.System, "Cutscenes", "Movie Subtitles", "System Configuration > Sound Settings",
            "Subtitle language for movies. Use the current value after choosing it."));
        list.Add(Toggle("CutsceneMovieOpening", ConfigSection.System, "Cutscenes", "Opening Movie", "System Configuration",
            "Play the opening movie."));

        list.Add(Flt("MouseSpeed", ConfigSection.System, "Camera", "Mouse Speed", camera, "Cursor speed. Stored as a decimal, matching the Mouse Settings slider."));
        list.Add(Toggle("MouseAutoFocus", ConfigSection.System, "Camera", "Focus on Hover", camera, "Focus the client when the cursor moves over the window."));
        list.Add(Toggle("MouseOpeLimit", ConfigSection.System, "Camera", "Confine Cursor", camera, "Keep the cursor inside the game window."));
        list.Add(Toggle("SystemMouseOperationSoftOn", ConfigSection.System, "Camera", "Software Cursor", camera, "Draw the software cursor instead of the operating system cursor."));
        list.Add(Toggle("SystemMouseOperationTrajectory", ConfigSection.System, "Camera", "Cursor Trail", camera, "Show the cursor trail."));
        list.Add(Toggle("SystemMouseOperationCursorScaling", ConfigSection.System, "Camera", "Scale Cursor with UI", camera, "Scale the cursor when the UI size changes."));
        list.Add(Toggle("IdlingCameraAFK", ConfigSection.System, "Camera", "Idle Camera When Away", "System Configuration",
            "Start the idle camera while you are away from the keyboard."));
        list.Add(Num("IdlingCameraSwitchType", ConfigSection.Ui, "Camera", "Idle Camera", "Character Configuration > Control Settings > Camera",
            "When the idle camera takes over. Use the current value."));
        list.Add(Num("CameraZoom", ConfigSection.System, "Camera", "Camera Zoom Limit", "Character Configuration > Control Settings > Camera",
            "Internal camera distance limit. Use the current value after setting zoom in the camera options."));
        list.Add(Num("EventCameraAutoControl", ConfigSection.Ui, "Camera", "Event Camera", "Character Configuration > Control Settings > Camera",
            "Automatic camera during events. Use the current value."));
        list.Add(Num("LegacyCameraType", ConfigSection.Ui, "Camera", "Legacy Camera", "Character Configuration > Control Settings > Camera",
            "Legacy camera type. Use the current value."));

        return list.ToArray();
    }

    private static void Vols(List<SettingInfo> list, string menu, string label, string volume, string toggle, string about)
    {
        list.Add(Vol(volume, ConfigSection.System, "Sound", label, menu, about + " 0 is silent and 100 is full.", 0, 100));
        list.Add(Toggle(toggle, ConfigSection.System, "Sound", "Enable " + label, menu, "Checkbox beside " + label + " in Sound Settings."));
    }

    private static void AddDisp(List<SettingInfo> list, string option, string label) =>
        list.Add(Choice(option, ConfigSection.Ui, "Nameplates", label, "Character Configuration > Display Name Settings",
            "When this nameplate is shown. Same numbers as /nameplatedisp: 1 Always, 2 During Battle, 5 Out of Combat, 3 When Targeted, 4 Never.",
            NameDisp));

    private static void AddName(List<SettingInfo> list, string option, string label) =>
        list.Add(Choice(option, ConfigSection.Ui, "Nameplates", label, "Character Configuration > Display Name Settings",
            "How the name is written. Same numbers as /nameplatetype: 1 Full Name, 2 Surname Abbreviated, 3 Forename Abbreviated, 4 Initials.",
            NameType));

    private static SettingInfo Toggle(string option, ConfigSection section, string group, string label, string menu, string about) =>
        new() { Option = option, Section = section, Group = group, Label = label, Menu = menu, About = about, Kind = SettingKind.Toggle };

    private static SettingInfo Vol(string option, ConfigSection section, string group, string label, string menu, string about, int min, int max) =>
        new() { Option = option, Section = section, Group = group, Label = label, Menu = menu, About = about, Kind = SettingKind.Volume, Min = min, Max = max };

    private static SettingInfo Num(string option, ConfigSection section, string group, string label, string menu, string about) =>
        new() { Option = option, Section = section, Group = group, Label = label, Menu = menu, About = about, Kind = SettingKind.Number };

    private static SettingInfo Flt(string option, ConfigSection section, string group, string label, string menu, string about) =>
        new() { Option = option, Section = section, Group = group, Label = label, Menu = menu, About = about, Kind = SettingKind.Float };

    private static SettingInfo Choice(string option, ConfigSection section, string group, string label, string menu, string about, (string, string)[] choices) =>
        new() { Option = option, Section = section, Group = group, Label = label, Menu = menu, About = about, Kind = SettingKind.Choice, Choices = choices };
}
