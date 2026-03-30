using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Origo.GodotAdapter.Serialization;

public sealed class Vector2JsonConverter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Vector2.");

        float x = 0, y = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.X:
                    x = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.X, nameof(Vector2));
                    break;
                case GodotJsonPropertyNames.Y:
                    y = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.Y, nameof(Vector2));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Vector2(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(GodotJsonPropertyNames.X, value.X);
        writer.WriteNumber(GodotJsonPropertyNames.Y, value.Y);
        writer.WriteEndObject();
    }
}

public sealed class Vector2IJsonConverter : JsonConverter<Vector2I>
{
    public override Vector2I Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Vector2I.");

        int x = 0, y = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.X:
                    x = GodotJsonReaderStrict.ReadInt32(ref reader, GodotJsonPropertyNames.X, nameof(Vector2I));
                    break;
                case GodotJsonPropertyNames.Y:
                    y = GodotJsonReaderStrict.ReadInt32(ref reader, GodotJsonPropertyNames.Y, nameof(Vector2I));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Vector2I(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vector2I value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(GodotJsonPropertyNames.X, value.X);
        writer.WriteNumber(GodotJsonPropertyNames.Y, value.Y);
        writer.WriteEndObject();
    }
}

public sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Vector3.");

        float x = 0, y = 0, z = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.X:
                    x = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.X, nameof(Vector3));
                    break;
                case GodotJsonPropertyNames.Y:
                    y = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.Y, nameof(Vector3));
                    break;
                case GodotJsonPropertyNames.Z:
                    z = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.Z, nameof(Vector3));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Vector3(x, y, z);
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(GodotJsonPropertyNames.X, value.X);
        writer.WriteNumber(GodotJsonPropertyNames.Y, value.Y);
        writer.WriteNumber(GodotJsonPropertyNames.Z, value.Z);
        writer.WriteEndObject();
    }
}

public sealed class Vector3IJsonConverter : JsonConverter<Vector3I>
{
    public override Vector3I Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Vector3I.");

        int x = 0, y = 0, z = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.X:
                    x = GodotJsonReaderStrict.ReadInt32(ref reader, GodotJsonPropertyNames.X, nameof(Vector3I));
                    break;
                case GodotJsonPropertyNames.Y:
                    y = GodotJsonReaderStrict.ReadInt32(ref reader, GodotJsonPropertyNames.Y, nameof(Vector3I));
                    break;
                case GodotJsonPropertyNames.Z:
                    z = GodotJsonReaderStrict.ReadInt32(ref reader, GodotJsonPropertyNames.Z, nameof(Vector3I));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Vector3I(x, y, z);
    }

    public override void Write(Utf8JsonWriter writer, Vector3I value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(GodotJsonPropertyNames.X, value.X);
        writer.WriteNumber(GodotJsonPropertyNames.Y, value.Y);
        writer.WriteNumber(GodotJsonPropertyNames.Z, value.Z);
        writer.WriteEndObject();
    }
}

public sealed class Vector4JsonConverter : JsonConverter<Vector4>
{
    public override Vector4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Vector4.");

        float x = 0, y = 0, z = 0, w = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.X:
                    x = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.X, nameof(Vector4));
                    break;
                case GodotJsonPropertyNames.Y:
                    y = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.Y, nameof(Vector4));
                    break;
                case GodotJsonPropertyNames.Z:
                    z = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.Z, nameof(Vector4));
                    break;
                case GodotJsonPropertyNames.W:
                    w = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.W, nameof(Vector4));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Vector4(x, y, z, w);
    }

    public override void Write(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(GodotJsonPropertyNames.X, value.X);
        writer.WriteNumber(GodotJsonPropertyNames.Y, value.Y);
        writer.WriteNumber(GodotJsonPropertyNames.Z, value.Z);
        writer.WriteNumber(GodotJsonPropertyNames.W, value.W);
        writer.WriteEndObject();
    }
}
