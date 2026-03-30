using System;
using System.Collections.Generic;
using System.Text.Json;
using Origo.Core.Abstractions;
using Origo.Core.Logging;
using Origo.Core.Utils;

namespace Origo.Core.Snd;

/// <summary>
///     管理与 SND 相关的运行时映射：
///     场景资源别名 → 引擎具体资源路径，以及模板别名 → SndMetaData 模板。
///     作为实例挂载在 <see cref="SndWorld" /> 上，随运行时生命周期管理。
/// </summary>
internal sealed class SndMappings
{
    private readonly Dictionary<string, string> _sceneAliases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _templatePaths = new(StringComparer.Ordinal);
    private SndTemplateResolver? _templateResolver;

    /// <summary>
    ///     从指定文本文件加载场景资源别名映射。
    ///     文件格式为按行的 <c>key: value</c>，忽略空行与以 # 开头的注释行。
    /// </summary>
    public void LoadSceneAliases(IFileSystem fileSystem, string mapFilePath, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);
        _sceneAliases.Clear();

        if (string.IsNullOrWhiteSpace(mapFilePath))
            throw new ArgumentException("Scene alias map file path cannot be null or whitespace.", nameof(mapFilePath));
        if (!fileSystem.Exists(mapFilePath))
            throw new InvalidOperationException($"Scene resource alias map file '{mapFilePath}' not found.");

        foreach (var kv in KeyValueFileParser.Parse(fileSystem.ReadAllText(mapFilePath), mapFilePath, strict: true,
                     logger))
            _sceneAliases[kv.Key] = kv.Value;
        logger.Log(LogLevel.Info, nameof(SndMappings),
            new LogMessageBuilder().AddSuffix("filePath", mapFilePath)
                .Build($"Loaded {_sceneAliases.Count} scene resource aliases."));
    }

    /// <summary>
    ///     将节点资源标识解析为具体资源路径。
    ///     严格模式：若不是显式资源路径（例如 res://、user://）且别名不存在则抛异常。
    /// </summary>
    public string ResolveSceneAlias(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Scene id cannot be null or whitespace.", nameof(id));

        if (IsExplicitResourcePath(id))
            return id;

        if (_sceneAliases.TryGetValue(id, out var mapped))
            return mapped;

        throw new KeyNotFoundException($"Scene alias '{id}' not found in scene alias map.");
    }

    /// <summary>
    ///     从映射文件加载模板别名到 JSON 文件路径的映射，并配置内部使用的文件系统与 JSON 选项。
    /// </summary>
    public void LoadTemplates(
        IFileSystem fileSystem,
        string mapFilePath,
        JsonSerializerOptions jsonOptions,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(jsonOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _templatePaths.Clear();
        _templateResolver = null;

        if (string.IsNullOrWhiteSpace(mapFilePath))
            throw new ArgumentException("Snd template alias map file path cannot be null or whitespace.",
                nameof(mapFilePath));
        if (!fileSystem.Exists(mapFilePath))
            throw new InvalidOperationException($"Snd template alias map file '{mapFilePath}' not found.");

        foreach (var kv in KeyValueFileParser.Parse(fileSystem.ReadAllText(mapFilePath), mapFilePath, strict: true,
                     logger))
            _templatePaths[kv.Key] = kv.Value;

        _templateResolver = new SndTemplateResolver(fileSystem, jsonOptions, _templatePaths);
        logger.Log(LogLevel.Info, nameof(SndMappings),
            new LogMessageBuilder().AddSuffix("filePath", mapFilePath)
                .Build($"Loaded {_templatePaths.Count} Snd templates."));
    }

    /// <summary>
    ///     按别名解析并加载一个 SndMetaData 模板（严格模式：缺失/未初始化/解析失败直接抛异常）。
    /// </summary>
    public SndMetaData ResolveTemplate(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Template alias cannot be null or whitespace.", nameof(alias));

        if (_templateResolver is null)
            throw new InvalidOperationException(
                "Template resolution called before LoadTemplates; template paths are not initialized.");

        if (_templatePaths.Count == 0)
            throw new InvalidOperationException(
                "No templates loaded: the template map is empty. Call LoadTemplates with a map that contains at least one template entry before resolving template references.");

        return _templateResolver.Resolve(alias);
    }

    /// <summary>
    ///     将 JSON 数组元素（可能包含模板引用简写）解析为 SndMetaData 列表。
    ///     支持两种形式：完整的 SndMetaData JSON 对象，或 { "sndName": "...", "templateKey": "..." } 简写。
    /// </summary>
    public IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(
        JsonElement root,
        JsonSerializerOptions jsonOptions)
    {
        var list = new List<SndMetaData>();

        foreach (var item in root.EnumerateArray())
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("templateKey", out var templateKeyProp))
            {
                var templateKey = templateKeyProp.GetString();
                if (string.IsNullOrWhiteSpace(templateKey))
                    throw new InvalidOperationException("Config entry has an empty 'templateKey'.");

                var sndName = item.TryGetProperty("sndName", out var sndNameProp)
                    ? sndNameProp.GetString() ?? string.Empty
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(sndName))
                    throw new InvalidOperationException(
                        $"Config entry referencing template '{templateKey}' has an empty 'sndName'.");

                var template = ResolveTemplate(templateKey);

                var cloned = template.DeepClone();
                cloned.Name = sndName;
                list.Add(cloned);
            }
            else
            {
                var meta = item.Deserialize<SndMetaData>(jsonOptions);
                if (meta is null)
                    throw new InvalidOperationException("Failed to deserialize SndMetaData from config entry.");
                if (string.IsNullOrWhiteSpace(meta.Name))
                    throw new InvalidOperationException("SndMetaData 'name' cannot be empty.");
                list.Add(meta);
            }

        return list;
    }

    /// <summary>Detects Godot-style schemes (<c>res://</c>, <c>user://</c>) and other URI-like resource ids.</summary>
    private const string UriLikeSchemeSeparator = "://";

    private static bool IsExplicitResourcePath(string id)
    {
        return id.Contains(UriLikeSchemeSeparator, StringComparison.Ordinal);
    }

}