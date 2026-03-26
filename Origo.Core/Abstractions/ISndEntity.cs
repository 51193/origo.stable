using System;
using System.Collections.Generic;

namespace Origo.Core.Abstractions;

/// <summary>
///     抽象 SND 实体的最小接口，使策略与数据层不依赖具体引擎节点类型。
/// </summary>
public interface ISndEntity
{
    /// <summary>
    ///     稳定的实体名（对应 <see cref="Snd.SndMetaData.Name" />），可用于场景内查找与跨系统引用。
    /// </summary>
    string Name { get; }

    void SetData<T>(string name, T value);

    T GetData<T>(string name);

    (bool found, T value) TryGetData<T>(string name);

    /// <summary>
    ///     订阅指定键的数据变更通知。
    /// </summary>
    void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter = null);

    /// <summary>
    ///     取消订阅指定键的数据变更通知。
    /// </summary>
    void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback);

    INodeHandle? GetNode(string name);

    IReadOnlyCollection<string> GetNodeNames();

    void AddStrategy(string index);

    void RemoveStrategy(string index);
}