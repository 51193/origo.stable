using System;
using System.Text.Json;
namespace Origo.GodotAdapter.Serialization;

/// <summary>
///     Wraps Utf8JsonReader primitive reads as <see cref="JsonException" /> with property context.
/// </summary>
internal static class GodotJsonReaderStrict
{
    internal static float ReadSingle(ref Utf8JsonReader reader, string propertyName, string typeName)
    {
        try
        {
            return reader.GetSingle();
        }
        catch (Exception ex)
        {
            throw new JsonException($"Expected JSON number for property '{propertyName}' on {typeName}.", ex);
        }
    }

    internal static int ReadInt32(ref Utf8JsonReader reader, string propertyName, string typeName)
    {
        try
        {
            return reader.GetInt32();
        }
        catch (Exception ex)
        {
            throw new JsonException($"Expected JSON integer for property '{propertyName}' on {typeName}.", ex);
        }
    }

    internal static double ReadDouble(ref Utf8JsonReader reader, string propertyName, string typeName)
    {
        try
        {
            return reader.GetDouble();
        }
        catch (Exception ex)
        {
            throw new JsonException($"Expected JSON number for property '{propertyName}' on {typeName}.", ex);
        }
    }

    internal static T DeserializeChild<T>(ref Utf8JsonReader reader, JsonSerializerOptions options, string propertyName,
        string parentType)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(ref reader, options)
                   ?? throw new JsonException($"Expected value for property '{propertyName}' on {parentType}.");
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to deserialize property '{propertyName}' on {parentType}.", ex);
        }
    }
}
