using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Origo.GodotAdapter.Serialization;

public sealed class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Color.");

        float r = 0, g = 0, b = 0, a = 1;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.R:
                    r = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.R, nameof(Color));
                    break;
                case GodotJsonPropertyNames.G:
                    g = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.G, nameof(Color));
                    break;
                case GodotJsonPropertyNames.B:
                    b = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.B, nameof(Color));
                    break;
                case GodotJsonPropertyNames.A:
                    a = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.A, nameof(Color));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Color(r, g, b, a);
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(GodotJsonPropertyNames.R, value.R);
        writer.WriteNumber(GodotJsonPropertyNames.G, value.G);
        writer.WriteNumber(GodotJsonPropertyNames.B, value.B);
        writer.WriteNumber(GodotJsonPropertyNames.A, value.A);
        writer.WriteEndObject();
    }
}

public sealed class Rect2JsonConverter : JsonConverter<Rect2>
{
    public override Rect2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Rect2.");

        var position = Vector2.Zero;
        var size = Vector2.Zero;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.Position:
                    position = GodotJsonReaderStrict.DeserializeChild<Vector2>(ref reader, options,
                        GodotJsonPropertyNames.Position, nameof(Rect2));
                    break;
                case GodotJsonPropertyNames.Size:
                    size = GodotJsonReaderStrict.DeserializeChild<Vector2>(ref reader, options,
                        GodotJsonPropertyNames.Size, nameof(Rect2));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Rect2(position, size);
    }

    public override void Write(Utf8JsonWriter writer, Rect2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(GodotJsonPropertyNames.Position);
        JsonSerializer.Serialize(writer, value.Position, options);

        writer.WritePropertyName(GodotJsonPropertyNames.Size);
        JsonSerializer.Serialize(writer, value.Size, options);

        writer.WriteEndObject();
    }
}

public sealed class Rect2IJsonConverter : JsonConverter<Rect2I>
{
    public override Rect2I Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Rect2I.");

        var position = Vector2I.Zero;
        var size = Vector2I.Zero;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.Position:
                    position = GodotJsonReaderStrict.DeserializeChild<Vector2I>(ref reader, options,
                        GodotJsonPropertyNames.Position, nameof(Rect2I));
                    break;
                case GodotJsonPropertyNames.Size:
                    size = GodotJsonReaderStrict.DeserializeChild<Vector2I>(ref reader, options,
                        GodotJsonPropertyNames.Size, nameof(Rect2I));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Rect2I(position, size);
    }

    public override void Write(Utf8JsonWriter writer, Rect2I value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(GodotJsonPropertyNames.Position);
        JsonSerializer.Serialize(writer, value.Position, options);

        writer.WritePropertyName(GodotJsonPropertyNames.Size);
        JsonSerializer.Serialize(writer, value.Size, options);

        writer.WriteEndObject();
    }
}

public sealed class AabbJsonConverter : JsonConverter<Aabb>
{
    public override Aabb Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Aabb.");

        var position = Vector3.Zero;
        var size = Vector3.Zero;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.Position:
                    position = GodotJsonReaderStrict.DeserializeChild<Vector3>(ref reader, options,
                        GodotJsonPropertyNames.Position, nameof(Aabb));
                    break;
                case GodotJsonPropertyNames.Size:
                    size = GodotJsonReaderStrict.DeserializeChild<Vector3>(ref reader, options,
                        GodotJsonPropertyNames.Size, nameof(Aabb));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Aabb(position, size);
    }

    public override void Write(Utf8JsonWriter writer, Aabb value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(GodotJsonPropertyNames.Position);
        JsonSerializer.Serialize(writer, value.Position, options);

        writer.WritePropertyName(GodotJsonPropertyNames.Size);
        JsonSerializer.Serialize(writer, value.Size, options);

        writer.WriteEndObject();
    }
}

public sealed class PlaneJsonConverter : JsonConverter<Plane>
{
    public override Plane Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Plane.");

        var normal = Vector3.Up;
        float d = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case GodotJsonPropertyNames.Normal:
                    normal = GodotJsonReaderStrict.DeserializeChild<Vector3>(ref reader, options,
                        GodotJsonPropertyNames.Normal, nameof(Plane));
                    break;
                case GodotJsonPropertyNames.D:
                    d = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.D, nameof(Plane));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Plane(normal, d);
    }

    public override void Write(Utf8JsonWriter writer, Plane value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(GodotJsonPropertyNames.Normal);
        JsonSerializer.Serialize(writer, value.Normal, options);

        writer.WriteNumber(GodotJsonPropertyNames.D, value.D);

        writer.WriteEndObject();
    }
}
