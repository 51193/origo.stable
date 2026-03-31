using System.Collections.Generic;
using Origo.Core.Snd;

namespace Origo.Core.Abstractions;

/// <summary>
///     面向 Core 的 SND 场景宿主抽象。
///     在 ISndSceneAccess 的基础上，补充对实体集合与按元数据生成实体的操作。
/// </summary>
public interface ISndSceneHost : ISndSceneAccess
{
    /// <summary>
    ///     从一份元数据生成并加入一个实体，返回该实体的抽象接口。
    ///     具体的节点创建与挂载由实现负责。
    /// </summary>
    ISndEntity Spawn(SndMetaData metaData);

    /// <summary>
    ///     获取当前场景中所有仍然存活的实体视图。
    /// </summary>
    IReadOnlyCollection<ISndEntity> GetEntities();

    /// <summary>
    ///     根据实体名称查找对应实体。
    /// </summary>
    ISndEntity? FindByName(string name);
}
