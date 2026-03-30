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
                case GodotJsonPropertyNames.X:
                    x = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.X, nameof(Quaternion));
                    break;
                case GodotJsonPropertyNames.Y:
                    y = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.Y, nameof(Quaternion));
                    break;
                case GodotJsonPropertyNames.Z:
                    z = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.Z, nameof(Quaternion));
                    break;
                case GodotJsonPropertyNames.W:
                    w = GodotJsonReaderStrict.ReadSingle(ref reader, GodotJsonPropertyNames.W, nameof(Quaternion));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Quaternion(x, y, z, w);
    }

    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(GodotJsonPropertyNames.X, value.X);
        writer.WriteNumber(GodotJsonPropertyNames.Y, value.Y);
        writer.WriteNumber(GodotJsonPropertyNames.Z, value.Z);
        writer.WriteNumber(GodotJsonPropertyNames.W, value.W);
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
                case GodotJsonPropertyNames.X:
                    x = GodotJsonReaderStrict.DeserializeChild<Vector3>(ref reader, options, GodotJsonPropertyNames.X,
                        nameof(Basis));
                    break;
                case GodotJsonPropertyNames.Y:
                    y = GodotJsonReaderStrict.DeserializeChild<Vector3>(ref reader, options, GodotJsonPropertyNames.Y,
                        nameof(Basis));
                    break;
                case GodotJsonPropertyNames.Z:
                    z = GodotJsonReaderStrict.DeserializeChild<Vector3>(ref reader, options, GodotJsonPropertyNames.Z,
                        nameof(Basis));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Basis(x, y, z);
    }

    public override void Write(Utf8JsonWriter writer, Basis value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(GodotJsonPropertyNames.X);
        JsonSerializer.Serialize(writer, value.X, options);

        writer.WritePropertyName(GodotJsonPropertyNames.Y);
        JsonSerializer.Serialize(writer, value.Y, options);

        writer.WritePropertyName(GodotJsonPropertyNames.Z);
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
                case GodotJsonPropertyNames.BasisProperty:
                    basis = GodotJsonReaderStrict.DeserializeChild<Basis>(ref reader, options,
                        GodotJsonPropertyNames.BasisProperty, nameof(Transform3D));
                    break;
                case GodotJsonPropertyNames.OriginProperty:
                    origin = GodotJsonReaderStrict.DeserializeChild<Vector3>(ref reader, options,
                        GodotJsonPropertyNames.OriginProperty, nameof(Transform3D));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Transform3D(basis, origin);
    }

    public override void Write(Utf8JsonWriter writer, Transform3D value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(GodotJsonPropertyNames.BasisProperty);
        JsonSerializer.Serialize(writer, value.Basis, options);

        writer.WritePropertyName(GodotJsonPropertyNames.OriginProperty);
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
                case GodotJsonPropertyNames.X:
                    x = GodotJsonReaderStrict.DeserializeChild<Vector2>(ref reader, options, GodotJsonPropertyNames.X,
                        nameof(Transform2D));
                    break;
                case GodotJsonPropertyNames.Y:
                    y = GodotJsonReaderStrict.DeserializeChild<Vector2>(ref reader, options, GodotJsonPropertyNames.Y,
                        nameof(Transform2D));
                    break;
                case GodotJsonPropertyNames.OriginProperty:
                    origin = GodotJsonReaderStrict.DeserializeChild<Vector2>(ref reader, options,
                        GodotJsonPropertyNames.OriginProperty, nameof(Transform2D));
                    break;
                default: reader.Skip(); break;
            }
        }

        return new Transform2D(x, y, origin);
    }

    public override void Write(Utf8JsonWriter writer, Transform2D value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(GodotJsonPropertyNames.X);
        JsonSerializer.Serialize(writer, value.X, options);

        writer.WritePropertyName(GodotJsonPropertyNames.Y);
        JsonSerializer.Serialize(writer, value.Y, options);

        writer.WritePropertyName(GodotJsonPropertyNames.OriginProperty);
        JsonSerializer.Serialize(writer, value.Origin, options);

        writer.WriteEndObject();
    }
}
