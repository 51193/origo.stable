using System.Collections.Generic;
using Origo.Core.Snd;

namespace Origo.Core.Abstractions;

/// <summary>
///     抽象 SND 节点容器行为，负责节点恢复、查询、回收与导出。
/// </summary>
internal interface INodeHost
{
    INodeHandle GetNode(string name);

    IReadOnlyCollection<string> GetNodeNames();

    void Recover(NodeMetaData metaData);

    void Release();

    NodeMetaData SerializeMetaData();
}
