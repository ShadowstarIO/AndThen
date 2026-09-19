using System;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AndThen;

internal static class ChatSender
{
    public static bool TrySend(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        if (!Plugin.ClientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer is null) return false;
        if (!command.StartsWith('/')) command = "/" + command;
        if (IsSelf(command)) return true;

        try
        {
            unsafe
            {
                var ui = UIModule.Instance();
                if (ui == null) return false;
                var message = Utf8String.FromString(command);
                if (message == null) return false;
                ui->ProcessChatBoxEntry(message);
                message->Dtor(true);
                return true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Command failed: {Command}", command);
            return false;
        }
    }

    public static string? StatusCommand(string value) => value.Trim().ToLowerInvariant() switch
    {
        "online" => "/busy off",
        "away" or "afk" => "/away on",
        "busy" => "/busy on",
        "roleplaying" or "rp" => "/roleplaying on",
        "lookingtomeld" or "meld" => "/lookingtomeld on",
        "lookingforparty" or "lfp" => "/lookingforparty on",
        _ => null,
    };

    private static bool IsSelf(string command)
    {
        var t = command.Trim();
        return t.StartsWith("/atn", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("/andthen", StringComparison.OrdinalIgnoreCase);
    }
}
