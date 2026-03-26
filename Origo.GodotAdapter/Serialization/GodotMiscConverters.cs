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
                case "r": r = reader.GetSingle(); break;
                case "g": g = reader.GetSingle(); break;
                case "b": b = reader.GetSingle(); break;
                case "a": a = reader.GetSingle(); break;
                default: reader.Skip(); break;
            }
        }

        return new Color(r, g, b, a);
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("r", value.R);
        writer.WriteNumber("g", value.G);
        writer.WriteNumber("b", value.B);
        writer.WriteNumber("a", value.A);
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
                case "position": position = JsonSerializer.Deserialize<Vector2>(ref reader, options); break;
                case "size": size = JsonSerializer.Deserialize<Vector2>(ref reader, options); break;
                default: reader.Skip(); break;
            }
        }

        return new Rect2(position, size);
    }

    public override void Write(Utf8JsonWriter writer, Rect2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("position");
        JsonSerializer.Serialize(writer, value.Position, options);

        writer.WritePropertyName("size");
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
                case "position": position = JsonSerializer.Deserialize<Vector2I>(ref reader, options); break;
                case "size": size = JsonSerializer.Deserialize<Vector2I>(ref reader, options); break;
                default: reader.Skip(); break;
            }
        }

        return new Rect2I(position, size);
    }

    public override void Write(Utf8JsonWriter writer, Rect2I value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("position");
        JsonSerializer.Serialize(writer, value.Position, options);

        writer.WritePropertyName("size");
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
                case "position": position = JsonSerializer.Deserialize<Vector3>(ref reader, options); break;
                case "size": size = JsonSerializer.Deserialize<Vector3>(ref reader, options); break;
                default: reader.Skip(); break;
            }
        }

        return new Aabb(position, size);
    }

    public override void Write(Utf8JsonWriter writer, Aabb value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("position");
        JsonSerializer.Serialize(writer, value.Position, options);

        writer.WritePropertyName("size");
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
                case "normal": normal = JsonSerializer.Deserialize<Vector3>(ref reader, options); break;
                case "d": d = reader.GetSingle(); break;
                default: reader.Skip(); break;
            }
        }

        return new Plane(normal, d);
    }

    public override void Write(Utf8JsonWriter writer, Plane value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("normal");
        JsonSerializer.Serialize(writer, value.Normal, options);

        writer.WriteNumber("d", value.D);

        writer.WriteEndObject();
    }
}