using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.Core.Blackboard;

/// <summary>
///     默认的内存黑板实现，使用 TypedData 存储键值对以保留类型信息。
/// </summary>
public sealed class Blackboard : IBlackboard
{
    private readonly Dictionary<string, TypedData> _data = new(StringComparer.Ordinal);

    public void Set<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        _data[key] = new TypedData(typeof(T), value);
    }

    public (bool found, T value) TryGet<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return (false, default!);

        if (_data.TryGetValue(key, out var typedData) && typedData.Data is T value)
            return (true, value);

        return (false, default!);
    }

    public T GetOrDefault<T>(string key, T defaultValue = default!)
    {
        var (found, value) = TryGet<T>(key);
        return found ? value : defaultValue;
    }

    public void Clear()
    {
        _data.Clear();
    }

    public IReadOnlyCollection<string> GetKeys()
    {
        return _data.Keys;
    }

    public IReadOnlyDictionary<string, TypedData> ExportAll()
    {
        return new Dictionary<string, TypedData>(_data, StringComparer.Ordinal);
    }

    public void ImportAll(IReadOnlyDictionary<string, TypedData> data)
    {
        _data.Clear();
        foreach (var pair in data)
            _data[pair.Key] = pair.Value;
    }
}