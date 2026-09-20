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
    public static string ToJsonAll(IEnumerable<ThenRule> rules) => JsonSerializer.Serialize(rules, Json);

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
        if (!TryDecodeMany(text, out var many, out error) || many.Count == 0) return false;
        rule = many[0];
        return true;
    }

    public static bool TryDecodeMany(string text, out List<ThenRule> rules, out string error)
    {
        rules = [];
        error = string.Empty;
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = "Clipboard is empty.";
            return false;
        }

        try
        {
            if (text.StartsWith('['))
            {
                var many = JsonSerializer.Deserialize<List<ThenRule>>(text, Json);
                if (many is { Count: > 0 }) rules.AddRange(many);
            }
            else if (text.StartsWith('{'))
            {
                var one = JsonSerializer.Deserialize<ThenRule>(text, Json);
                if (one is not null) rules.Add(one);
            }
            else
            {
                foreach (var part in text.Split(['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!TryOneCode(part, out var rule) || rule is null) continue;
                    rules.Add(rule);
                }
            }

            if (rules.Count == 0)
            {
                error = "No rules in that share.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryOneCode(string text, out ThenRule? rule)
    {
        rule = null;
        try
        {
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
            return rule is not null;
        }
        catch
        {
            return false;
        }
    }
}
