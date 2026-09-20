using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace AndThen;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;
    public bool Enabled { get; set; } = true;
    public bool NotifyInChat { get; set; } = true;
    public bool OpenUiOnLoad { get; set; }
    public bool QuietInCutscene { get; set; } = true;
    public float PollSec { get; set; } = 0.5f;
    public List<ThenRule> Rules { get; set; } = [];
    public List<string> Folders { get; set; } = [];
    public List<string> MutedFolders { get; set; } = [];

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
