using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace AndThen;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 6;
    public bool Enabled { get; set; } = true;
    public bool NotifyInChat { get; set; } = true;
    public bool OpenUiOnLoad { get; set; }
    public bool QuietInCutscene { get; set; } = true;
    public bool QuietInCombat { get; set; }
    public bool QuietInDuty { get; set; }
    public bool QuietBetweenAreas { get; set; } = true;
    public bool QuietWhenOccupied { get; set; } = true;
    public bool QuietInGPose { get; set; }
    public bool QuietWhenDead { get; set; } = true;
    public bool QuietWhenCrafting { get; set; }
    public bool QuietWhenPerforming { get; set; }
    public bool QuietWhenTrading { get; set; }
    public float PollSec { get; set; } = 0.5f;
    public float DefaultDelaySec { get; set; }
    public List<ThenRule> Rules { get; set; } = [];
    public List<string> Folders { get; set; } = [];

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
