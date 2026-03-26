using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Origo.Core.Serialization;

namespace Origo.Core.Snd.Serialization;

/// <summary>
///     将 TypedData 在 JSON 中表示为 { "type": "...", "data": ... }。
///     读取时不依赖属性顺序：先解析完整对象，再按 type 反序列化 data。
/// </summary>
public sealed class TypedDataJsonConverter : JsonConverter<TypedData>
{
    private readonly TypeStringMapping _typeMapping;

    public TypedDataJsonConverter(TypeStringMapping typeMapping)
    {
        _typeMapping = typeMapping ?? throw new ArgumentNullException(nameof(typeMapping));
    }

    public override TypedData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Expected JSON object for TypedData.");

        if (!root.TryGetProperty("type", out var typeEl))
            throw new JsonException("Missing 'type' property for TypedData.");

        var typeName = typeEl.GetString();
        if (string.IsNullOrEmpty(typeName))
            throw new JsonException("TypedData 'type' must be a non-empty string.");

        var dataType = _typeMapping.GetTypeByName(typeName)
                       ?? throw new JsonException($"Unknown type name '{typeName}'.");

        if (!root.TryGetProperty("data", out var dataEl))
            return new TypedData(dataType, null);

        if (dataEl.ValueKind == JsonValueKind.Null)
            return new TypedData(dataType, null);

        object? data;
        try
        {
            data = dataEl.Deserialize(dataType, options);
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to deserialize TypedData 'data' for type '{typeName}'.", ex);
        }

        return new TypedData(dataType, data);
    }

    public override void Write(Utf8JsonWriter writer, TypedData value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        var typeName = _typeMapping.GetNameByType(value.DataType);
        if (typeName == null)
            throw new JsonException($"Type '{value.DataType.FullName}' is not registered in TypeStringMapping.");

        writer.WriteString("type", typeName);

        writer.WritePropertyName("data");
        if (value.Data is not null)
            JsonSerializer.Serialize(writer, value.Data, value.DataType, options);
        else
            writer.WriteNullValue();

        writer.WriteEndObject();
    }
}