using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Origo.Core.Snd.Serialization;

/// <summary>
///     SndMetaData 的 JSON 表示为:
///     { "name": "...", "node": {..}, "strategy": {..}, "data": {..} }
///     与 W2 的结构保持一致但不依赖具体引擎类型。
/// </summary>
public sealed class SndMetaDataJsonConverter : JsonConverter<SndMetaData>
{
    public override SndMetaData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for SndMetaData.");

        var result = new SndMetaData();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "name":
                        result.Name = reader.GetString() ?? string.Empty;
                        break;
                    case "node":
                        result.NodeMetaData = JsonSerializer.Deserialize<NodeMetaData>(ref reader, options);
                        break;
                    case "strategy":
                        result.StrategyMetaData = JsonSerializer.Deserialize<StrategyMetaData>(ref reader, options);
                        break;
                    case "data":
                        result.DataMetaData = JsonSerializer.Deserialize<DataMetaData>(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, SndMetaData value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("name", value.Name);

        writer.WritePropertyName("node");
        if (value.NodeMetaData is not null)
            JsonSerializer.Serialize(writer, value.NodeMetaData, options);
        else
            writer.WriteNullValue();

        writer.WritePropertyName("strategy");
        if (value.StrategyMetaData is not null)
            JsonSerializer.Serialize(writer, value.StrategyMetaData, options);
        else
            writer.WriteNullValue();

        writer.WritePropertyName("data");
        if (value.DataMetaData is not null)
            JsonSerializer.Serialize(writer, value.DataMetaData, options);
        else
            writer.WriteNullValue();

        writer.WriteEndObject();
    }
}