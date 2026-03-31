using System;
using System.Collections.Generic;

namespace Origo.Core.Snd;

/// <summary>
///     SND 实体在框架层面的元数据聚合。
///     与具体引擎无关，仅包含名称、节点元信息、策略列表与数据。
/// </summary>
public sealed class SndMetaData
{
    public string Name { get; set; } = string.Empty;

    public NodeMetaData? NodeMetaData { get; set; }

    public StrategyMetaData? StrategyMetaData { get; set; }

    public DataMetaData? DataMetaData { get; set; } = new();

    /// <summary>
    ///     克隆元数据容器（新字典与列表；<see cref="TypedData" /> 实例与其中 <c>Data</c> 引用与 JSON 往返深拷贝类似，不递归复制对象图）。
    /// </summary>
    public SndMetaData DeepClone()
    {
        return new SndMetaData
        {
            Name = Name,
            NodeMetaData = NodeMetaData is null
                ? null
                : new NodeMetaData
                {
                    Pairs = new Dictionary<string, string>(NodeMetaData.Pairs, StringComparer.Ordinal)
                },
            StrategyMetaData = StrategyMetaData is null
                ? null
                : new StrategyMetaData { Indices = new List<string>(StrategyMetaData.Indices) },
            DataMetaData = DataMetaData is null
                ? null
                : new DataMetaData
                {
                    Pairs = new Dictionary<string, TypedData>(DataMetaData.Pairs)
                }
        };
    }
}
