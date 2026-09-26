using System;
using System.Diagnostics;
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
            ThenKind.Logout => ChatSender.TrySend("/logout"),
            ThenKind.Exit => ExitGame(),
            ThenKind.PenumbraMod => PenumbraIpc.Apply(row),
            ThenKind.PenumbraReset => PenumbraIpc.Reset(row),
            ThenKind.Wait => true,
            _ => false,
        };
    }

    private static bool ExitGame()
    {
        try
        {
            Process.GetCurrentProcess().Kill();
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Close Game failed");
            return false;
        }
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
        if (option.Length == 0 || raw.Length == 0) return false;
        try
        {
            if (!SettingGuide.TryCoerce(option, raw, out var number, out var floating, out var isFloat))
            {
                Plugin.Log.Warning("Config value not recognized: {Option}={Value}", option, raw);
                return false;
            }

            if (row.Section == ConfigSection.Ui)
            {
                if (!Enum.TryParse<UiConfigOption>(option, true, out var ui)) return false;
                Write(ui, number, floating, isFloat);
                return true;
            }

            if (!Enum.TryParse<SystemConfigOption>(option, true, out var sys)) return false;
            Write(sys, number, floating, isFloat);
            WriteTwin(sys, number, floating, isFloat);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Config failed: {Option}={Value}", option, raw);
            return false;
        }
    }

    private static void Write(SystemConfigOption option, uint number, float floating, bool isFloat)
    {
        if (isFloat) Plugin.GameConfig.Set(option, floating);
        else Plugin.GameConfig.Set(option, number);
    }

    private static void Write(UiConfigOption option, uint number, float floating, bool isFloat)
    {
        if (isFloat) Plugin.GameConfig.Set(option, floating);
        else Plugin.GameConfig.Set(option, number);
    }

    private static void WriteTwin(SystemConfigOption option, uint number, float floating, bool isFloat)
    {
        var name = option.ToString();
        var otherName = name.EndsWith("_DX11", StringComparison.Ordinal) ? name[..^5] : name + "_DX11";
        if (!Enum.TryParse<SystemConfigOption>(otherName, out var other) || other.Equals(option)) return;
        try
        {
            Write(other, number, floating, isFloat);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Config twin failed: {Option}", otherName);
        }
    }
}