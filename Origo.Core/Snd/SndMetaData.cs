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
}