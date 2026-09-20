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
    Group = 8,
    Time = 9,
    Weather = 10,
    Nearby = 11,
    Place = 12,
    Target = 13,
    OnlineStatus = 14,
    Account = 15,
    Housing = 16,
    Mount = 17,
    Level = 18,
}

public enum ThenKind
{
    Command = 0,
    Wait = 1,
    Status = 2,
    Config = 3,
    Notify = 4,
    Logout = 5,
    Exit = 6,
    PenumbraMod = 7,
    PenumbraReset = 8,
}

public enum ApplyMode
{
    Off = 0,
    Dialog = 1,
    Auto = 2,
}

public enum ConfigSection
{
    System = 0,
    Ui = 1,
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
        ChipKind.Group => $"Group {Value}",
        ChipKind.Time => $"Time {Value}",
        ChipKind.Weather => $"Weather {Value}",
        ChipKind.Nearby => $"Nearby {Value}",
        ChipKind.Place => $"Place {Value}",
        ChipKind.Target => $"Target {Value}",
        ChipKind.OnlineStatus => $"Status {Value}",
        ChipKind.Account => $"Account {Value}",
        ChipKind.Housing => $"Housing {Value}",
        ChipKind.Mount => $"Mount {Value}",
        ChipKind.Level => $"Level {Value}",
        _ => Value,
    };
}

[Serializable]
public class ThenRow
{
    public ThenKind Kind { get; set; } = ThenKind.Command;
    public ConfigSection Section { get; set; }
    public string Option { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int WaitMs { get; set; }
    public bool Flag { get; set; } = true;
    public bool Inherit { get; set; }
    public bool Permanent { get; set; }
    public int Number { get; set; }
    public string Extra { get; set; } = string.Empty;

    public string Label => Kind switch
    {
        ThenKind.Wait => WaitMs <= 0 ? "Wait" : $"Wait {WaitMs} ms",
        ThenKind.Status => $"Online Status {Value}",
        ThenKind.Config => string.IsNullOrWhiteSpace(Option) ? "Game Setting" : $"{Option} = {Value}",
        ThenKind.Notify => string.IsNullOrWhiteSpace(Value) ? "Chat Notice" : $"Say {Value}",
        ThenKind.Logout => "Log Out",
        ThenKind.Exit => "Close Game",
        ThenKind.PenumbraMod => PenumbraLabel(),
        ThenKind.PenumbraReset => string.IsNullOrWhiteSpace(Option) ? "Penumbra Reset Temp" : $"Penumbra Reset {Value}",
        _ => string.IsNullOrWhiteSpace(Value) ? "/command" : Value,
    };

    private string PenumbraLabel()
    {
        var name = string.IsNullOrWhiteSpace(Value) ? Option : Value;
        if (string.IsNullOrWhiteSpace(name)) return "Penumbra Mod";
        var how = Permanent ? "set" : "temp";
        var state = Inherit ? "inherit" : Flag ? "on" : "off";
        return $"Penumbra {how} {name} {state}";
    }
}

[Serializable]
public class ThenRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Rule";
    public string Notes { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public ApplyMode Mode { get; set; } = ApplyMode.Off;
    public float DelaySec { get; set; }
    public List<RuleChip> AndChips { get; set; } = [];
    public List<RuleChip> OrChips { get; set; } = [];
    public List<RuleChip> NotChips { get; set; } = [];
    public List<ThenRow> Then { get; set; } = [];

    public string FolderKey => string.IsNullOrWhiteSpace(Folder) ? string.Empty : Folder.Trim();
    public string CommandToken => (Name ?? string.Empty).Trim();
}

[Serializable]
public class AppliedLine
{
    public DateTime At { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
