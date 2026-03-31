using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions;
using Origo.Core.DataSource;
using Origo.Core.Serialization;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd;

/// <summary>
///     面向上层游戏的 SND 统一入口，封装策略池、序列化配置与映射。
/// </summary>
public sealed class SndWorld
{
    private readonly ILogger _logger;

    public SndWorld(
        TypeStringMapping typeMapping,
        ILogger logger,
        DataSourceConverterRegistry registry,
        IDataSourceCodec jsonCodec,
        IDataSourceCodec mapCodec)
    {
        ArgumentNullException.ThrowIfNull(typeMapping);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(jsonCodec);
        ArgumentNullException.ThrowIfNull(mapCodec);
        _logger = logger;
        StrategyPool = new SndStrategyPool(logger);
        TypeMapping = typeMapping;
        ConverterRegistry = registry;
        JsonCodec = jsonCodec;
        MapCodec = mapCodec;
        Mappings = new SndMappings();
    }

    /// <summary>
    ///     策略对象池，管理所有已注册策略的创建、共享与引用计数。
    /// </summary>
    internal SndStrategyPool StrategyPool { get; }

    /// <summary>
    ///     类型名与 .NET 类型之间的双向映射，用于 TypedData 的序列化。
    ///     适配层可在启动时注册引擎特有类型。
    /// </summary>
    internal TypeStringMapping TypeMapping { get; }

    /// <summary>
    ///     数据源转换器注册表，负责 DataSourceNode 与强类型 C# 对象之间的双向转换。
    /// </summary>
    public DataSourceConverterRegistry ConverterRegistry { get; }

    /// <summary>
    ///     JSON 编解码器，用于 DataSourceNode 与 JSON 文本之间的双向转换。
    ///     注意：尽管名称包含 'Json'，实际编解码通过 IDataSourceCodec 接口抽象，此属性在创建时注入。
    /// </summary>
    public IDataSourceCodec JsonCodec { get; }

    /// <summary>
    ///     Map 格式编解码器，用于 key: value 格式的简单文本文件（如 meta.map）。
    /// </summary>
    internal IDataSourceCodec MapCodec { get; }

    /// <summary>
    ///     SND 映射管理器，维护场景别名与模板别名的映射关系。
    /// </summary>
    internal SndMappings Mappings { get; }

    public void RegisterStrategy<TStrategy>(Func<TStrategy> factory) where TStrategy : BaseStrategy =>
        StrategyPool.Register(factory);

    public void RegisterTypeMappings(Action<TypeStringMapping> registerMappings)
    {
        ArgumentNullException.ThrowIfNull(registerMappings);
        registerMappings(TypeMapping);
    }

    public SndMetaData ResolveTemplate(string alias) => Mappings.ResolveTemplate(alias);

    /// <summary>
    ///     克隆 SND 元数据（与模板解析路径一致，便于将来统一替换实现）。
    /// </summary>
    public static SndMetaData CloneMetaData(SndMetaData meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        return meta.DeepClone();
    }

    public void LoadSceneAliases(IFileSystem fileSystem, string mapFilePath, ILogger logger) =>
        Mappings.LoadSceneAliases(fileSystem, mapFilePath, logger);

    public void LoadTemplates(IFileSystem fileSystem, string mapFilePath, ILogger logger) =>
        Mappings.LoadTemplates(fileSystem, mapFilePath, JsonCodec, ConverterRegistry, logger);

    public IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(DataSourceNode root) =>
        Mappings.ResolveMetaListFromJsonArray(root, ConverterRegistry);

    public IReadOnlyDictionary<string, TypedData> DeserializeTypedDataMap(string serializedText)
    {
        ArgumentNullException.ThrowIfNull(serializedText);
        var node = JsonCodec.Decode(serializedText);
        return ConverterRegistry.Read<IReadOnlyDictionary<string, TypedData>>(node);
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
        var node = ConverterRegistry.Write(metaData);
        return JsonCodec.Encode(node);
    }

    public SndMetaData DeserializeMeta(string serializedText)
    {
        ArgumentNullException.ThrowIfNull(serializedText);
        var node = JsonCodec.Decode(serializedText);
        return ConverterRegistry.Read<SndMetaData>(node);
    }

    public string SerializeMetaList(IEnumerable<SndMetaData> metaDataList)
    {
        var list = metaDataList as IReadOnlyList<SndMetaData> ?? metaDataList.ToList();
        var node = ConverterRegistry.Write(list);
        return JsonCodec.Encode(node);
    }

    public IReadOnlyList<SndMetaData> DeserializeMetaList(string serializedText)
    {
        ArgumentNullException.ThrowIfNull(serializedText);
        var node = JsonCodec.Decode(serializedText);
        return ConverterRegistry.Read<IReadOnlyList<SndMetaData>>(node);
    }
}
