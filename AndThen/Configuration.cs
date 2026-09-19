using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace AndThen;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool NotifyInChat { get; set; } = true;
    public bool OpenUiOnLoad { get; set; }
    public int PollMs { get; set; } = 250;
    public List<ThenRule> Rules { get; set; } = DefaultRules();

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    private static List<ThenRule> DefaultRules() =>
    [
        new()
        {
            Name = "Duty start",
            Notes = "Lower load when an instance starts",
            Folder = "Examples",
            Enabled = false,
            Priority = 100,
            AndChips = [new() { Kind = ChipKind.State, Value = "InDuty" }],
            NotChips = [new() { Kind = ChipKind.Job, Value = "MNK" }],
            Then =
            [
                new() { Kind = ThenKind.Setting, Setting = SettingKey.Fps, Value = "30" },
                new() { Kind = ThenKind.Setting, Setting = SettingKey.MouseLock, Value = "on" },
                new() { Kind = ThenKind.Setting, Setting = SettingKey.MusicOn, Value = "off" },
                new() { Kind = ThenKind.Setting, Setting = SettingKey.DisplayHead, Value = "on" },
                new() { Kind = ThenKind.Setting, Setting = SettingKey.DisplayWeapon, Value = "on" },
                new() { Kind = ThenKind.Setting, Setting = SettingKey.HudLayout, Value = "2" },
                new() { Kind = ThenKind.Status, Value = "Busy" },
                new() { Kind = ThenKind.Command, Value = "/autolockon on" },
            ],
        },
        new()
        {
            Name = "Leave duty",
            Notes = "Restore overworld settings",
            Folder = "Examples",
            Enabled = false,
            Priority = 90,
            NotChips = [new() { Kind = ChipKind.State, Value = "InDuty" }],
            Then =
            [
                new() { Kind = ThenKind.Setting, Setting = SettingKey.Fps, Value = "none" },
                new() { Kind = ThenKind.Setting, Setting = SettingKey.MouseLock, Value = "off" },
                new() { Kind = ThenKind.Setting, Setting = SettingKey.MusicOn, Value = "on" },
                new() { Kind = ThenKind.Status, Value = "Online" },
            ],
        },
    ];
}
