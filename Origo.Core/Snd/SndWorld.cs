using System;
using System.Collections.Generic;
using System.Text.Json;
using Origo.Core.Abstractions;
using Origo.Core.Serialization;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd;

/// <summary>
///     面向上层游戏的 SND 统一入口，封装策略池、序列化配置与映射。
/// </summary>
public sealed class SndWorld
{
    private readonly ILogger _logger;

    public SndWorld(TypeStringMapping typeMapping, ILogger logger, Action<JsonSerializerOptions>? configureJson = null)
    {
        ArgumentNullException.ThrowIfNull(typeMapping);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        StrategyPool = new SndStrategyPool(logger);
        TypeMapping = typeMapping;
        JsonOptions = OrigoJson.CreateDefaultOptions(TypeMapping, configureJson);
        Mappings = new SndMappings();
    }

    internal SndStrategyPool StrategyPool { get; }

    /// <summary>
    ///     类型名与 .NET 类型之间的双向映射，用于 TypedData 的序列化。
    ///     适配层可在启动时注册引擎特有类型。
    /// </summary>
    internal TypeStringMapping TypeMapping { get; }

    internal JsonSerializerOptions JsonOptions { get; }

    internal SndMappings Mappings { get; }

    public void RegisterStrategy<TStrategy>(Func<TStrategy> factory) where TStrategy : BaseStrategy
    {
        StrategyPool.Register(factory);
    }

    public void RegisterTypeMappings(Action<TypeStringMapping> registerMappings)
    {
        ArgumentNullException.ThrowIfNull(registerMappings);
        registerMappings(TypeMapping);
    }

    public SndMetaData ResolveTemplate(string alias)
    {
        return Mappings.ResolveTemplate(alias);
    }

    /// <summary>
    ///     克隆 SND 元数据（与模板解析路径一致，便于将来统一替换实现）。
    /// </summary>
    public static SndMetaData CloneMetaData(SndMetaData meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        return meta.DeepClone();
    }

    public void LoadSceneAliases(IFileSystem fileSystem, string mapFilePath, ILogger logger)
    {
        Mappings.LoadSceneAliases(fileSystem, mapFilePath, logger);
    }

    public void LoadTemplates(IFileSystem fileSystem, string mapFilePath, ILogger logger)
    {
        Mappings.LoadTemplates(fileSystem, mapFilePath, JsonOptions, logger);
    }

    public IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(JsonElement root)
    {
        return Mappings.ResolveMetaListFromJsonArray(root, JsonOptions);
    }

    public IReadOnlyDictionary<string, TypedData> DeserializeTypedDataMap(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, TypedData>>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize typed data map.");
    }

    public SndEntity CreateEntity(
        INodeFactory nodeFactory,
        SndContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return new SndEntity(nodeFactory, StrategyPool, Mappings, context, logger);
    }

    public string SerializeMeta(SndMetaData metaData)
    {
        return OrigoJson.SerializeSndMetaData(metaData, JsonOptions);
    }

    public SndMetaData DeserializeMeta(string json)
    {
        return OrigoJson.DeserializeSndMetaData(json, JsonOptions);
    }

    public string SerializeMetaList(IEnumerable<SndMetaData> metaDataList)
    {
        return OrigoJson.SerializeSndMetaDataList(metaDataList, JsonOptions);
    }

    public IReadOnlyList<SndMetaData> DeserializeMetaList(string json)
    {
        return OrigoJson.DeserializeSndMetaDataList(json, JsonOptions);
    }
}