using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.Core.Tests;

internal sealed class TestLogger : ILogger
{
    public readonly List<string> Infos = new();
    public readonly List<string> Warnings = new();
    public readonly List<string> Errors = new();

    public void Log(LogLevel level, string tag, string message)
    {
        switch (level)
        {
            case LogLevel.Warning:
                Warnings.Add($"{tag}: {message}");
                break;
            case LogLevel.Error:
                Errors.Add($"{tag}: {message}");
                break;
            default:
                Infos.Add($"{tag}: {message}");
                break;
        }
    }
}

internal sealed class TestNodeHandle : INodeHandle
{
    public TestNodeHandle(string name, object? native = null)
    {
        Name = name;
        Native = native ?? new object();
    }

    public string Name { get; }
    public object Native { get; }
    public bool IsVisible { get; private set; } = true;
    public int FreeCount { get; private set; }

    public void Free() => FreeCount++;
    public void SetVisible(bool visible) => IsVisible = visible;
}

internal sealed class TestNodeFactory : INodeFactory
{
    private readonly HashSet<string> _resourceIdsThatFail;

    public TestNodeFactory(IEnumerable<string>? resourceIdsThatFail = null)
    {
        _resourceIdsThatFail = resourceIdsThatFail != null
            ? new HashSet<string>(resourceIdsThatFail, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    public readonly List<(string logicalName, string resourceId)> Requests = new();
    public readonly List<TestNodeHandle> CreatedHandles = new();

    public INodeHandle? Create(string logicalName, string resourceId)
    {
        Requests.Add((logicalName, resourceId));
        if (_resourceIdsThatFail.Contains(resourceId))
            return null;

        var handle = new TestNodeHandle(logicalName, resourceId);
        CreatedHandles.Add(handle);
        return handle;
    }
}

internal sealed class TestFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

    public void SeedFile(string path, string content)
    {
        var normalized = Normalize(path);
        _files[normalized] = content;
        EnsureParents(normalized);
    }

    public bool Exists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path)
    {
        var normalized = Normalize(path).TrimEnd('/');
        return _directories.Contains(normalized) ||
               _files.Keys.Any(f => f.StartsWith(normalized + "/", StringComparison.Ordinal));
    }

    public string ReadAllText(string path)
    {
        var normalized = Normalize(path);
        return _files[normalized];
    }

    public void WriteAllText(string path, string content, bool overwrite)
    {
        var normalized = Normalize(path);
        if (!overwrite && _files.ContainsKey(normalized))
            throw new IOException($"File already exists: {normalized}");

        _files[normalized] = content;
        EnsureParents(normalized);
    }

    public void Copy(string sourcePath, string destinationPath, bool overwrite)
    {
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        if (!_files.TryGetValue(source, out var content))
            throw new FileNotFoundException("Source not found.", source);

        if (!overwrite && _files.ContainsKey(destination))
            throw new IOException($"File already exists: {destination}");

        _files[destination] = content;
        EnsureParents(destination);
    }

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        var normalized = Normalize(directoryPath).TrimEnd('/');
        var prefix = normalized + "/";
        foreach (var file in _files.Keys.ToArray())
        {
            if (!file.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (!recursive)
            {
                var rest = file.Substring(prefix.Length);
                if (rest.Contains('/'))
                    continue;
            }

            if (searchPattern is "*" or "*.*" || file.EndsWith(searchPattern.TrimStart('*'), StringComparison.Ordinal))
                yield return file;
        }
    }

    public void CreateDirectory(string directoryPath)
    {
        var normalized = Normalize(directoryPath).TrimEnd('/');
        if (normalized.Length == 0)
            return;

        _directories.Add(normalized);
        EnsureParents(normalized + "/dummy");
    }

    public void Delete(string path)
    {
        var normalized = Normalize(path);
        _files.Remove(normalized);
    }

    public string CombinePath(string basePath, string relativePath)
    {
        return Normalize($"{Normalize(basePath).TrimEnd('/')}/{relativePath}");
    }

    public string GetParentDirectory(string path)
    {
        var normalized = Normalize(path).TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        return index <= 0 ? string.Empty : normalized.Substring(0, index);
    }

    public IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        var normalized = Normalize(directoryPath).TrimEnd('/');
        var prefix = normalized + "/";
        var children = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dir in _directories)
        {
            if (!dir.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var rest = dir.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            if (slash >= 0)
                rest = rest.Substring(0, slash);
            if (rest.Length > 0)
                children.Add(prefix + rest);
        }

        foreach (var file in _files.Keys.ToArray())
        {
            if (!file.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var rest = file.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            if (slash > 0)
                children.Add(prefix + rest.Substring(0, slash));
        }

        return children;
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private void EnsureParents(string filePath)
    {
        var normalized = Normalize(filePath);
        var index = normalized.LastIndexOf('/');
        while (index > 0)
        {
            var dir = normalized.Substring(0, index);
            _directories.Add(dir);
            index = dir.LastIndexOf('/');
        }
    }
}

internal sealed class TestSndSceneHost : ISndSceneHost
{
    private readonly List<SndMetaData> _metaList = new();
    private readonly List<ISndEntity> _entities = new();
    public int ClearAllCount { get; private set; }

    public ISndEntity Spawn(SndMetaData metaData)
    {
        _metaList.Add(metaData);
        var entity = new DummySndEntity(metaData.Name);
        _entities.Add(entity);
        return entity;
    }

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

    public ISndEntity? FindByName(string name)
    {
        return _entities.FirstOrDefault(e =>
        {
            var (found, value) = e.TryGetData<string>("name");
            return found && value == name;
        });
    }

    public IReadOnlyList<SndMetaData> ExportMetaList() => _metaList.ToArray();

    public void LoadFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        _metaList.Clear();
        _metaList.AddRange(metaList);
    }

    public void ClearAll()
    {
        ClearAllCount++;
        _metaList.Clear();
        _entities.Clear();
    }
}

internal sealed class DummySndEntity : ISndEntity
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);
    public readonly string EntityName;

    public DummySndEntity(string entityName)
    {
        EntityName = entityName;
        _data["name"] = entityName;
    }

    public string Name => EntityName;

    public void SetData<T>(string name, T value) => _data[name] = value;
    public T GetData<T>(string name) => _data.TryGetValue(name, out var value) && value is T cast ? cast : default!;
    public (bool found, T value) TryGetData<T>(string name)
    {
        if (_data.TryGetValue(name, out var value) && value is T cast)
            return (true, cast);
        return (false, default!);
    }

    public void Subscribe(string name, Action<ISndEntity, object?, object?> callback, Func<ISndEntity, object?, object?, bool>? filter = null)
    {
    }

    public void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback)
    {
    }

    public INodeHandle? GetNode(string name) => null;
    public IReadOnlyCollection<string> GetNodeNames() => Array.Empty<string>();
    public void AddStrategy(string index) { }
    public void RemoveStrategy(string index) { }
}
