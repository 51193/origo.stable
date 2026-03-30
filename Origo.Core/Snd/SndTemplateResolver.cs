using System;
using System.Collections.Generic;
using System.Text.Json;
using Origo.Core.Abstractions;
using Origo.Core.Serialization;

namespace Origo.Core.Snd;

/// <summary>
///     在已加载的模板路径映射上解析 <see cref="SndMetaData" />，带内存缓存。
/// </summary>
internal sealed class SndTemplateResolver
{
    private readonly Dictionary<string, SndMetaData> _cache = new(StringComparer.Ordinal);
    private readonly IFileSystem _fileSystem;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, string> _paths;

    public SndTemplateResolver(
        IFileSystem fileSystem,
        JsonSerializerOptions jsonOptions,
        Dictionary<string, string> templatePaths)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(jsonOptions);
        _fileSystem = fileSystem;
        _jsonOptions = jsonOptions;
        _paths = new Dictionary<string, string>(templatePaths, StringComparer.Ordinal);
    }

    public SndMetaData Resolve(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Template alias cannot be null or whitespace.", nameof(alias));

        if (_cache.TryGetValue(alias, out var cached))
            return cached;

        if (!_paths.TryGetValue(alias, out var path))
            throw new KeyNotFoundException($"Template alias '{alias}' not found in template map.");

        var json = _fileSystem.ReadAllText(path);
        var meta = OrigoJson.DeserializeSndMetaData(json, _jsonOptions);
        if (meta is null)
            throw new InvalidOperationException($"Template '{alias}' at '{path}' deserialized to null.");

        _cache[alias] = meta;
        return meta;
    }
}
