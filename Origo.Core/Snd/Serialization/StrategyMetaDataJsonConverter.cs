using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Origo.Core.Snd.Serialization;

/// <summary>
///     StrategyMetaData 的 JSON 表示为 { \"indices\": [ \"...\", ... ] }。
/// </summary>
public sealed class StrategyMetaDataJsonConverter : JsonConverter<StrategyMetaData>
{
    public override StrategyMetaData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for StrategyMetaData.");

        var result = new StrategyMetaData();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "indices":
                        result.Indices =
                            JsonSerializer.Deserialize<List<string>>(ref reader, options) ?? new List<string>();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, StrategyMetaData value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("indices");
        if (value.Indices is not null)
            JsonSerializer.Serialize(writer, value.Indices, options);
        else
            writer.WriteNullValue();

        writer.WriteEndObject();
    }
}