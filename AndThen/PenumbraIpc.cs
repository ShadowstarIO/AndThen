using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin.Ipc.Exceptions;

namespace AndThen;

internal sealed class PenumbraSnapshot
{
    public string Directory { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool Inherit { get; set; }
    public int Priority { get; set; }
    public bool Permanent { get; set; }
    public Dictionary<string, List<string>> Settings { get; set; } = [];

    public int GroupCount => Settings.Count;
}

internal static class PenumbraIpc
{
    public const string Source = "AndThen";
    public const int Key = 0x41544E31;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static DateTime listUntil;
    private static List<(string Dir, string Name)> cached = [];

    public static bool Available
    {
        get
        {
            try
            {
                return Plugin.PluginInterface.GetIpcSubscriber<bool>("Penumbra.GetEnabledState").InvokeFunc();
            }
            catch
            {
                return false;
            }
        }
    }

    public static IReadOnlyList<(string Dir, string Name)> Mods()
    {
        if (DateTime.Now < listUntil && cached.Count > 0) return cached;
        try
        {
            var raw = Plugin.PluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList").InvokeFunc()
                      ?? [];
            cached = raw
                .Select(kv => (Dir: kv.Key, Name: string.IsNullOrWhiteSpace(kv.Value) ? kv.Key : kv.Value))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            listUntil = DateTime.Now.AddSeconds(8);
        }
        catch (Exception ex)
        {
            Plugin.Log.Verbose(ex, "Penumbra GetModList failed");
            cached = [];
        }
        return cached;
    }

    public static string[] Labels() => Mods().Select(m => m.Name + "  ·  " + m.Dir).ToArray();

    public static bool TryParseLabel(string label, out string dir, out string name)
    {
        dir = string.Empty;
        name = string.Empty;
        var text = (label ?? string.Empty).Trim();
        if (text.Length == 0) return false;
        var i = text.LastIndexOf("  ·  ", StringComparison.Ordinal);
        if (i >= 0)
        {
            name = text[..i].Trim();
            dir = text[(i + 5)..].Trim();
            return dir.Length > 0;
        }
        foreach (var m in Mods())
        {
            if (m.Dir.Equals(text, StringComparison.OrdinalIgnoreCase) || m.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                dir = m.Dir;
                name = m.Name;
                return true;
            }
        }
        dir = text;
        name = text;
        return true;
    }

    public static Guid PlayerCollection()
    {
        try
        {
            var forObject = Plugin.PluginInterface
                .GetIpcSubscriber<int, (bool ObjectValid, bool Individual, (Guid Id, string Name) Collection)>("Penumbra.GetCollectionForObject.V5")
                .InvokeFunc(0);
            if (forObject.Collection.Id != Guid.Empty) return forObject.Collection.Id;
        }
        catch (IpcNotReadyError) { }
        catch (Exception ex) { Plugin.Log.Verbose(ex, "Penumbra GetCollectionForObject failed"); }

        try
        {
            var current = Plugin.PluginInterface
                .GetIpcSubscriber<byte, (Guid Id, string Name)?>("Penumbra.GetCollection")
                .InvokeFunc(0);
            if (current is { Id: var id } && id != Guid.Empty) return id;
        }
        catch (Exception ex) { Plugin.Log.Verbose(ex, "Penumbra GetCollection failed"); }

        return Guid.Empty;
    }

    public static bool TryRead(string dir, string name, out PenumbraSnapshot snap)
    {
        snap = new PenumbraSnapshot { Directory = dir, Name = name, Enabled = true };
        var collection = PlayerCollection();
        if (collection == Guid.Empty || string.IsNullOrWhiteSpace(dir)) return false;
        try
        {
            var result = Plugin.PluginInterface
                .GetIpcSubscriber<Guid, string, string, bool, (int Ec, (bool Enabled, int Priority, Dictionary<string, List<string>> Settings, bool Inherited)?)>("Penumbra.GetCurrentModSettings.V5")
                .InvokeFunc(collection, dir, name ?? string.Empty, false);
            if (result.Item2 is not { } t) return false;
            snap.Enabled = t.Enabled;
            snap.Priority = t.Priority;
            snap.Inherit = t.Inherited;
            snap.Settings = t.Settings ?? [];
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra GetCurrentModSettings failed for {Dir}", dir);
            return false;
        }
    }

    public static bool Apply(ThenRow row)
    {
        var snap = FromRow(row);
        if (string.IsNullOrWhiteSpace(snap.Directory)) return false;
        return snap.Permanent ? ApplyPermanent(snap) : ApplyTemporary(snap);
    }

    public static bool Reset(ThenRow row)
    {
        var dir = (row.Option ?? string.Empty).Trim();
        try
        {
            if (dir.Length == 0)
            {
                Plugin.PluginInterface
                    .GetIpcSubscriber<int, int, int>("Penumbra.RemoveAllTemporaryModSettingsPlayer.V5")
                    .InvokeFunc(0, Key);
                return true;
            }

            Plugin.PluginInterface
                .GetIpcSubscriber<int, string, string, int, int>("Penumbra.RemoveTemporaryModSettingsPlayer.V5")
                .InvokeFunc(0, dir, row.Value ?? string.Empty, Key);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra reset failed");
            return false;
        }
    }

    public static string Pack(PenumbraSnapshot snap) => JsonSerializer.Serialize(snap, Json);

    public static PenumbraSnapshot FromRow(ThenRow row)
    {
        var snap = new PenumbraSnapshot
        {
            Directory = (row.Option ?? string.Empty).Trim(),
            Name = (row.Value ?? string.Empty).Trim(),
            Enabled = row.Flag,
            Inherit = row.Inherit,
            Priority = row.Number,
            Permanent = row.Permanent,
        };
        if (!string.IsNullOrWhiteSpace(row.Extra))
        {
            try
            {
                var packed = JsonSerializer.Deserialize<PenumbraSnapshot>(row.Extra, Json);
                if (packed is not null)
                {
                    if (snap.Directory.Length == 0) snap.Directory = packed.Directory;
                    if (snap.Name.Length == 0) snap.Name = packed.Name;
                    snap.Settings = packed.Settings ?? [];
                }
            }
            catch { /* keep fields */ }
        }
        return snap;
    }

    public static ThenRow ToRow(PenumbraSnapshot snap) => new()
    {
        Kind = ThenKind.PenumbraMod,
        Option = snap.Directory,
        Value = snap.Name,
        Flag = snap.Enabled,
        Inherit = snap.Inherit,
        Number = snap.Priority,
        Permanent = snap.Permanent,
        Extra = Pack(snap),
    };

    private static bool ApplyTemporary(PenumbraSnapshot snap)
    {
        try
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> settings = snap.Settings.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value);
            var code = Plugin.PluginInterface
                .GetIpcSubscriber<int, string, string, (bool Inherit, bool Enabled, int Priority, IReadOnlyDictionary<string, IReadOnlyList<string>> Settings), string, int, int>(
                    "Penumbra.SetTemporaryModSettingsPlayer.V5")
                .InvokeFunc(0, snap.Directory, snap.Name, (snap.Inherit, snap.Enabled, snap.Priority, settings), Source, Key);
            if (code != 0)
                Plugin.Log.Verbose("Penumbra SetTemporaryModSettingsPlayer returned {Code} for {Dir}", code, snap.Directory);
            return code is 0 or 1;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra temporary apply failed for {Dir}", snap.Directory);
            return false;
        }
    }

    private static bool ApplyPermanent(PenumbraSnapshot snap)
    {
        var collection = PlayerCollection();
        if (collection == Guid.Empty) return false;
        try
        {
            var setMod = Plugin.PluginInterface.GetIpcSubscriber<Guid, string, string, bool, int>("Penumbra.TrySetMod.V5");
            var inherit = Plugin.PluginInterface.GetIpcSubscriber<Guid, string, string, bool, int>("Penumbra.TryInheritMod.V5");
            var priority = Plugin.PluginInterface.GetIpcSubscriber<Guid, string, string, int, int>("Penumbra.TrySetModPriority.V5");
            var settings = Plugin.PluginInterface.GetIpcSubscriber<Guid, string, string, string, IReadOnlyList<string>, int>("Penumbra.TrySetModSettings.V5");

            setMod.InvokeFunc(collection, snap.Directory, snap.Name, snap.Enabled);
            inherit.InvokeFunc(collection, snap.Directory, snap.Name, snap.Inherit);
            if (!snap.Inherit)
            {
                priority.InvokeFunc(collection, snap.Directory, snap.Name, snap.Priority);
                foreach (var (group, options) in snap.Settings)
                    settings.InvokeFunc(collection, snap.Directory, snap.Name, group, options);
            }
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Penumbra permanent apply failed for {Dir}", snap.Directory);
            return false;
        }
    }
}
