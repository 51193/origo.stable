using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Origo.GodotAdapter.Serialization;

public sealed class QuaternionJsonConverter : JsonConverter<Quaternion>
{
    public override Quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Quaternion.");

        float x = 0, y = 0, z = 0, w = 1;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case "x": x = reader.GetSingle(); break;
                case "y": y = reader.GetSingle(); break;
                case "z": z = reader.GetSingle(); break;
                case "w": w = reader.GetSingle(); break;
                default: reader.Skip(); break;
            }
        }

        return new Quaternion(x, y, z, w);
    }

    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteNumber("z", value.Z);
        writer.WriteNumber("w", value.W);
        writer.WriteEndObject();
    }
}

public sealed class BasisJsonConverter : JsonConverter<Basis>
{
    public override Basis Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Basis.");

        var x = Vector3.Right;
        var y = Vector3.Up;
        var z = Vector3.Back;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case "x": x = JsonSerializer.Deserialize<Vector3>(ref reader, options); break;
                case "y": y = JsonSerializer.Deserialize<Vector3>(ref reader, options); break;
                case "z": z = JsonSerializer.Deserialize<Vector3>(ref reader, options); break;
                default: reader.Skip(); break;
            }
        }

        return new Basis(x, y, z);
    }

    public override void Write(Utf8JsonWriter writer, Basis value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("x");
        JsonSerializer.Serialize(writer, value.X, options);

        writer.WritePropertyName("y");
        JsonSerializer.Serialize(writer, value.Y, options);

        writer.WritePropertyName("z");
        JsonSerializer.Serialize(writer, value.Z, options);

        writer.WriteEndObject();
    }
}

public sealed class Transform3DJsonConverter : JsonConverter<Transform3D>
{
    public override Transform3D Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Transform3D.");

        var basis = Basis.Identity;
        var origin = Vector3.Zero;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case "basis": basis = JsonSerializer.Deserialize<Basis>(ref reader, options); break;
                case "origin": origin = JsonSerializer.Deserialize<Vector3>(ref reader, options); break;
                default: reader.Skip(); break;
            }
        }

        return new Transform3D(basis, origin);
    }

    public override void Write(Utf8JsonWriter writer, Transform3D value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("basis");
        JsonSerializer.Serialize(writer, value.Basis, options);

        writer.WritePropertyName("origin");
        JsonSerializer.Serialize(writer, value.Origin, options);

        writer.WriteEndObject();
    }
}

public sealed class Transform2DJsonConverter : JsonConverter<Transform2D>
{
    public override Transform2D Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Transform2D.");

        var x = Vector2.Right;
        var y = Vector2.Down;
        var origin = Vector2.Zero;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();

            switch (prop)
            {
                case "x": x = JsonSerializer.Deserialize<Vector2>(ref reader, options); break;
                case "y": y = JsonSerializer.Deserialize<Vector2>(ref reader, options); break;
                case "origin": origin = JsonSerializer.Deserialize<Vector2>(ref reader, options); break;
                default: reader.Skip(); break;
            }
        }

        return new Transform2D(x, y, origin);
    }

    public override void Write(Utf8JsonWriter writer, Transform2D value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("x");
        JsonSerializer.Serialize(writer, value.X, options);

        writer.WritePropertyName("y");
        JsonSerializer.Serialize(writer, value.Y, options);

        writer.WritePropertyName("origin");
        JsonSerializer.Serialize(writer, value.Origin, options);

        writer.WriteEndObject();
    }
}