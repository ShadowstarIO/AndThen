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
            ThenKind.Setting => RunSetting(row.Setting, row.Value),
            ThenKind.Wait => true,
            _ => false,
        };
    }

    private static bool RunStatus(string value)
    {
        var cmd = ChatSender.StatusCommand(value);
        return cmd is not null && ChatSender.TrySend(cmd);
    }

    private static bool RunSetting(SettingKey key, string raw)
    {
        var value = (raw ?? string.Empty).Trim();
        try
        {
            switch (key)
            {
                case SettingKey.Fps:
                    Plugin.GameConfig.Set(SystemConfigOption.Fps, FpsValue(value));
                    return true;
                case SettingKey.MouseLock:
                    Plugin.GameConfig.Set(SystemConfigOption.MouseOpeLimit, On(value) ? 1u : 0u);
                    return true;
                case SettingKey.MusicOn:
                    return ChatSender.TrySend(On(value) ? "/bgm on" : "/bgm off");
                case SettingKey.SoundOn:
                    return ChatSender.TrySend(On(value) ? "/sound on" : "/sound off");
                case SettingKey.DisplayHead:
                    return ChatSender.TrySend(On(value) ? "/displayhead on" : "/displayhead off");
                case SettingKey.DisplayWeapon:
                    return ChatSender.TrySend(On(value) ? "/displayarms on" : "/displayarms off");
                case SettingKey.HudLayout:
                    if (!int.TryParse(value, out var hud)) hud = 1;
                    hud = Math.Clamp(hud, 1, 4);
                    return ChatSender.TrySend($"/hudlayout {hud}");
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Setting failed: {Key}={Value}", key, value);
            return false;
        }
    }

    private static uint FpsValue(string value) => value.ToLowerInvariant() switch
    {
        "none" or "off" or "0" or "max" or "unlimited" => 0u,
        "display" or "monitor" or "1" => 1u,
        "60" or "2" => 2u,
        "30" or "3" => 3u,
        "15" or "4" => 4u,
        _ => uint.TryParse(value, out var n) ? n : 0u,
    };

    private static bool On(string value) =>
        value.Equals("on", StringComparison.OrdinalIgnoreCase)
        || value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("1", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
}
