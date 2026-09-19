using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace AndThen;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public bool Enabled { get; set; } = true;
    public bool NotifyInChat { get; set; } = true;
    public bool OpenUiOnLoad { get; set; }
    public bool QuietInCutscene { get; set; } = true;
    public float PollSec { get; set; } = 0.5f;
    public List<ThenRule> Rules { get; set; } = DefaultRules();
    public List<string> Folders { get; set; } = ["Examples"];
    public List<string> MutedFolders { get; set; } = [];

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    private static List<ThenRule> DefaultRules() =>
    [
        new()
        {
            Name = "Duty start",
            Notes = "Lower load when an instance starts",
            Folder = "Examples",
            Enabled = true,
            Mode = ApplyMode.Off,
            AndChips = [new() { Kind = ChipKind.Duty, Value = "solid" }],
            Then =
            [
                new() { Kind = ThenKind.Config, Section = ConfigSection.System, Option = "Fps", Value = "3" },
                new() { Kind = ThenKind.Config, Section = ConfigSection.System, Option = "MouseOpeLimit", Value = "on" },
                new() { Kind = ThenKind.Config, Section = ConfigSection.Ui, Option = "BattleEffectOther", Value = "2" },
                new() { Kind = ThenKind.Status, Value = "Busy" },
            ],
        },
        new()
        {
            Name = "Leave duty",
            Notes = "Restore overworld settings",
            Folder = "Examples",
            Enabled = true,
            Mode = ApplyMode.Off,
            AndChips = [new() { Kind = ChipKind.Duty, Value = "none" }],
            Then =
            [
                new() { Kind = ThenKind.Config, Section = ConfigSection.System, Option = "Fps", Value = "0" },
                new() { Kind = ThenKind.Config, Section = ConfigSection.System, Option = "MouseOpeLimit", Value = "off" },
                new() { Kind = ThenKind.Config, Section = ConfigSection.Ui, Option = "BattleEffectOther", Value = "0" },
                new() { Kind = ThenKind.Status, Value = "Online" },
            ],
        },
        new()
        {
            Name = "City night",
            Notes = "Quieter presence after dusk in town",
            Folder = "Examples",
            Enabled = false,
            Mode = ApplyMode.Off,
            AndChips =
            [
                new() { Kind = ChipKind.Place, Value = "Town" },
                new() { Kind = ChipKind.Time, Value = "night" },
            ],
            Then =
            [
                new() { Kind = ThenKind.Status, Value = "Roleplaying" },
                new() { Kind = ThenKind.Notify, Value = "Night in town." },
            ],
        },
    ];
}
