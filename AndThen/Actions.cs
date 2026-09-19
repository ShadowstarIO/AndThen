using System;
using Dalamud.Game.Config;

namespace AndThen;

internal static class Actions
{
    public static bool Run(ThenRow row)
    {
        return row.Kind switch
        {
            ThenKind.Command => ChatSender.TrySend(row.Value),
            ThenKind.Status => RunStatus(row.Value),
            ThenKind.Config => RunConfig(row),
            ThenKind.Notify => RunNotify(row.Value),
            ThenKind.Wait => true,
            _ => false,
        };
    }

    private static bool RunNotify(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        Plugin.Notify(value.Trim());
        return true;
    }

    private static bool RunStatus(string value)
    {
        var cmd = ChatSender.StatusCommand(value);
        return cmd is not null && ChatSender.TrySend(cmd);
    }

    private static bool RunConfig(ThenRow row)
    {
        var option = (row.Option ?? string.Empty).Trim();
        var raw = (row.Value ?? string.Empty).Trim();
        if (option.Length == 0) return false;
        try
        {
            if (row.Section == ConfigSection.Ui)
            {
                if (!Enum.TryParse<UiConfigOption>(option, true, out var ui)) return false;
                return SetUi(ui, raw);
            }
            if (!Enum.TryParse<SystemConfigOption>(option, true, out var sys)) return false;
            return SetSys(sys, raw);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Config failed: {Option}={Value}", option, raw);
            return false;
        }
    }

    private static bool SetSys(SystemConfigOption option, string raw)
    {
        if (IsOnOff(raw, out var on)) { Plugin.GameConfig.Set(option, on); return true; }
        if (uint.TryParse(raw, out var n)) { Plugin.GameConfig.Set(option, n); return true; }
        if (float.TryParse(raw, out var f)) { Plugin.GameConfig.Set(option, f); return true; }
        Plugin.GameConfig.Set(option, MapNamed(raw));
        return true;
    }

    private static bool SetUi(UiConfigOption option, string raw)
    {
        if (IsOnOff(raw, out var on)) { Plugin.GameConfig.Set(option, on); return true; }
        if (uint.TryParse(raw, out var n)) { Plugin.GameConfig.Set(option, n); return true; }
        if (float.TryParse(raw, out var f)) { Plugin.GameConfig.Set(option, f); return true; }
        Plugin.GameConfig.Set(option, MapNamed(raw));
        return true;
    }

    private static bool IsOnOff(string raw, out bool on)
    {
        on = raw.Equals("on", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw == "1";
        return on
            || raw.Equals("off", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("false", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("no", StringComparison.OrdinalIgnoreCase)
            || raw == "0";
    }

    private static uint MapNamed(string raw) => raw.ToLowerInvariant() switch
    {
        "none" or "off" or "max" or "unlimited" or "all" => 0u,
        "limited" or "display" or "monitor" => 1u,
        "never" => 3u,
        "always" => 0u,
        _ => 0u,
    };
}
