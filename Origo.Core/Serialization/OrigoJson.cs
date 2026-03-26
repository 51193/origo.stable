using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Serialization;

namespace Origo.Core.Serialization;

/// <summary>
///     提供 Origo 核心使用的 JsonSerializerOptions 以及与 Snd 相关的 JSON 辅助方法。
///     不处理任何 I/O，仅在字符串与模型之间转换。
/// </summary>
public static class OrigoJson
{
    public static JsonSerializerOptions CreateDefaultOptions(
        TypeStringMapping typeMapping,
        Action<JsonSerializerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(typeMapping);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new TypedDataJsonConverter(typeMapping));
        options.Converters.Add(new DataMetaDataJsonConverter());
        options.Converters.Add(new StrategyMetaDataJsonConverter());
        options.Converters.Add(new SndMetaDataJsonConverter());

        configure?.Invoke(options);
        return options;
    }

    public static string SerializeSndMetaData(SndMetaData meta, JsonSerializerOptions options)
    {
        return JsonSerializer.Serialize(meta, options);
    }

    public static SndMetaData DeserializeSndMetaData(string json, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<SndMetaData>(json, options)
               ?? throw new JsonException("Failed to deserialize SndMetaData.");
    }

    public static string SerializeSndMetaDataList(IEnumerable<SndMetaData> metas, JsonSerializerOptions options)
    {
        return JsonSerializer.Serialize(metas, options);
    }

    public static IReadOnlyList<SndMetaData> DeserializeSndMetaDataList(string json, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<List<SndMetaData>>(json, options)
               ?? throw new JsonException("Failed to deserialize SndMetaData list.");
    }
}