using System;
using System.Collections.Generic;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Centralized codec for the session topology string format stored in
///     <see cref="Save.WellKnownKeys.SessionTopology" />.
///     Format: <c>key=levelId=syncProcess,key=levelId=syncProcess,...</c>.
/// </summary>
internal static class SessionTopologyCodec
{
    private const char EntrySeparator = ',';
    private const char FieldSeparator = '=';
    private const int RequiredFieldCount = 3;

    /// <summary>
    ///     Serializes a single topology entry into the canonical string form.
    /// </summary>
    public static string Serialize(string key, string levelId, bool syncProcess) =>
        $"{key}{FieldSeparator}{levelId}{FieldSeparator}{(syncProcess ? "true" : "false")}";

    /// <summary>
    ///     Joins multiple serialized entries into a single topology string.
    /// </summary>
    public static string Join(IEnumerable<string> entries) =>
        string.Join(EntrySeparator, entries);

    /// <summary>
    ///     Parses a topology string into a list of <see cref="SessionDescriptor" />.
    ///     Throws <see cref="InvalidOperationException" /> on malformed entries (explicit failure).
    /// </summary>
    public static List<SessionDescriptor> Parse(string raw)
    {
        var list = new List<SessionDescriptor>();
        var entries = raw.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries)
        {
            var parts = entry.Split(FieldSeparator);
            if (parts.Length < RequiredFieldCount)
                throw new InvalidOperationException(
                    $"Malformed session topology entry '{entry}': expected format 'key=levelId=syncProcess'.");

            var key = parts[0];
            var levelId = parts[1];
            var sync = bool.TryParse(parts[2], out var parsed) && parsed;

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(levelId))
                throw new InvalidOperationException(
                    $"Session topology entry '{entry}' has empty key or levelId.");

            list.Add(new SessionDescriptor(key, levelId, sync));
        }

        return list;
    }

    /// <summary>Lightweight descriptor for a session topology entry.</summary>
    internal readonly record struct SessionDescriptor(string Key, string LevelId, bool SyncProcess);
}
