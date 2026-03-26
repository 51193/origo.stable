using System.Collections.Generic;
using System.Text.Json;
using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.Core.Save;

/// <summary>
///     装饰器黑板：包装内部 <see cref="IBlackboard" />，每次 mutation 自动序列化到指定文件路径。
/// </summary>
public sealed class PersistentBlackboard : IBlackboard
{
    private readonly string _filePath;
    private readonly IFileSystem _fileSystem;
    private readonly IBlackboard _inner;
    private readonly JsonSerializerOptions _jsonOptions;

    public PersistentBlackboard(
        IFileSystem fileSystem,
        string filePath,
        JsonSerializerOptions jsonOptions,
        IBlackboard? inner = null)
    {
        _fileSystem = fileSystem;
        _filePath = filePath;
        _jsonOptions = jsonOptions;
        _inner = inner ?? new Blackboard.Blackboard();
    }

    public void Set<T>(string key, T value)
    {
        _inner.Set(key, value);
        Persist();
    }

    public (bool found, T value) TryGet<T>(string key)
    {
        return _inner.TryGet<T>(key);
    }

    public T GetOrDefault<T>(string key, T defaultValue = default!)
    {
        return _inner.GetOrDefault(key, defaultValue);
    }

    public void Clear()
    {
        _inner.Clear();
        Persist();
    }

    public IReadOnlyCollection<string> GetKeys()
    {
        return _inner.GetKeys();
    }

    public IReadOnlyDictionary<string, TypedData> ExportAll()
    {
        return _inner.ExportAll();
    }

    public void ImportAll(IReadOnlyDictionary<string, TypedData> data)
    {
        _inner.ImportAll(data);
        Persist();
    }

    /// <summary>
    ///     启动时从磁盘恢复状态。若文件不存在则不做任何操作。
    /// </summary>
    public void LoadFromDisk()
    {
        if (!_fileSystem.Exists(_filePath))
            return;

        var json = _fileSystem.ReadAllText(_filePath);
        var dict = JsonSerializer.Deserialize<Dictionary<string, TypedData>>(json, _jsonOptions);
        if (dict != null)
            _inner.ImportAll(dict);
    }

    private void Persist()
    {
        var parentDir = _fileSystem.GetParentDirectory(_filePath);
        if (!string.IsNullOrEmpty(parentDir) && !_fileSystem.DirectoryExists(parentDir))
            _fileSystem.CreateDirectory(parentDir);

        var data = _inner.ExportAll();
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        _fileSystem.WriteAllText(_filePath, json, true);
    }
}