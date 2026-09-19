using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace AndThen;

internal static class Share
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string ToJson(ThenRule rule) => JsonSerializer.Serialize(rule, Json);

    public static string Encode(ThenRule rule)
    {
        var raw = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rule, Json));
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.SmallestSize, true))
            gz.Write(raw, 0, raw.Length);
        return "AT1." + Convert.ToBase64String(ms.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string text, out ThenRule? rule, out string error)
    {
        rule = null;
        error = string.Empty;
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = "Clipboard is empty.";
            return false;
        }

        try
        {
            if (text.StartsWith('{') || text.StartsWith('['))
            {
                if (text.StartsWith('['))
                {
                    var many = JsonSerializer.Deserialize<List<ThenRule>>(text, Json);
                    rule = many is { Count: > 0 } ? many[0] : null;
                }
                else rule = JsonSerializer.Deserialize<ThenRule>(text, Json);
                if (rule is null) { error = "Empty share."; return false; }
                return true;
            }

            if (text.StartsWith("AT1.", StringComparison.OrdinalIgnoreCase))
                text = text[4..];
            text = text.Replace('-', '+').Replace('_', '/');
            while (text.Length % 4 != 0) text += "=";
            var bytes = Convert.FromBase64String(text);
            using var input = new MemoryStream(bytes);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            rule = JsonSerializer.Deserialize<ThenRule>(Encoding.UTF8.GetString(outMs.ToArray()), Json);
            if (rule is null) { error = "Empty share."; return false; }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
