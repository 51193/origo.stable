using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Origo.Core.Snd.Serialization;

/// <summary>
///     DataMetaData 的 JSON 表示为 { \"pairs\": { \"key\": {TypedData}, ... } }。
/// </summary>
public sealed class DataMetaDataJsonConverter : JsonConverter<DataMetaData>
{
    public override DataMetaData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for DataMetaData.");

        var result = new DataMetaData();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "pairs":
                        result.Pairs =
                            JsonSerializer.Deserialize<Dictionary<string, TypedData>>(ref reader, options)
                            ?? throw new JsonException("DataMetaData.pairs cannot be null.");
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, DataMetaData value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("pairs");
        if (value.Pairs is not null)
            JsonSerializer.Serialize(writer, value.Pairs, options);
        else
            writer.WriteNullValue();

        writer.WriteEndObject();
    }
}