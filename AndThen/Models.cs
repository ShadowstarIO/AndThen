using System;
using System.Collections.Generic;

namespace AndThen;

public enum ChipKind
{
    State = 0,
    Job = 1,
    Role = 2,
    Zone = 3,
    World = 4,
    DataCenter = 5,
    PartySize = 6,
    Duty = 7,
}

public enum ThenKind
{
    Command = 0,
    Wait = 1,
    Setting = 2,
    Status = 3,
}

public enum SettingKey
{
    Fps = 0,
    MouseLock = 1,
    MusicOn = 2,
    SoundOn = 3,
    DisplayHead = 4,
    DisplayWeapon = 5,
    HudLayout = 6,
}

public enum OnlineStatusAction
{
    LeaveAlone = 0,
    Online = 1,
    Away = 2,
    Busy = 3,
    Roleplaying = 4,
    LookingToMeld = 5,
    LookingForParty = 6,
}

[Serializable]
public class RuleChip
{
    public ChipKind Kind { get; set; }
    public string Value { get; set; } = string.Empty;

    public string Label => Kind switch
    {
        ChipKind.State => Value,
        ChipKind.Job => $"Job {Value}",
        ChipKind.Role => $"Role {Value}",
        ChipKind.Zone => $"Zone {Value}",
        ChipKind.World => $"World {Value}",
        ChipKind.DataCenter => $"DC {Value}",
        ChipKind.PartySize => $"Party {Value}",
        ChipKind.Duty => $"Duty {Value}",
        _ => Value,
    };
}

[Serializable]
public class ThenRow
{
    public ThenKind Kind { get; set; } = ThenKind.Command;
    public SettingKey Setting { get; set; }
    public string Value { get; set; } = string.Empty;
    public int WaitMs { get; set; }

    public string Label => Kind switch
    {
        ThenKind.Wait => WaitMs <= 0 ? "Wait" : $"Wait {WaitMs}ms",
        ThenKind.Setting => $"{Setting} = {Value}",
        ThenKind.Status => $"Status {Value}",
        _ => string.IsNullOrWhiteSpace(Value) ? "/command" : Value,
    };
}

[Serializable]
public class ThenRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New rule";
    public string Notes { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public List<RuleChip> AndChips { get; set; } = [];
    public List<RuleChip> OrChips { get; set; } = [];
    public List<RuleChip> NotChips { get; set; } = [];
    public List<ThenRow> Then { get; set; } = [];

    public string FolderKey => string.IsNullOrWhiteSpace(Folder) ? string.Empty : Folder.Trim();
}
