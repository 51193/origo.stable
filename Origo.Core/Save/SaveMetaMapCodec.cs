using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions;
using Origo.Core.Logging;

namespace Origo.Core.Save;

/// <summary>
///     解析/序列化存档元数据 map 文本，格式为按行 "key: value"。
/// </summary>
internal static class SaveMetaMapCodec
{
    public static IReadOnlyDictionary<string, string> Parse(
        string? content,
        ILogger? logger = null,
        string? sourceName = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new Dictionary<string, string>();

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                logger?.Log(LogLevel.Warning, nameof(SaveMetaMapCodec),
                    new LogMessageBuilder().AddSuffix("filePath", sourceName ?? "unknown")
                        .Build($"Invalid meta.map line '{line}'. Expected 'key: value'."));
                continue;
            }

            var key = parts[0];
            var value = parts[1];
            if (key.Length == 0 || value.Length == 0)
            {
                logger?.Log(LogLevel.Warning, nameof(SaveMetaMapCodec),
                    new LogMessageBuilder().AddSuffix("filePath", sourceName ?? "unknown")
                        .Build($"Invalid meta.map line '{line}'. Empty key or value."));
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    public static string Serialize(IReadOnlyDictionary<string, string>? map)
    {
        if (map == null || map.Count == 0)
            return string.Empty;

        return string.Join("\n", map.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}: {p.Value}"));
    }
}