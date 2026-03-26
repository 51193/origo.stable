using System.Collections.Generic;
using Origo.Core.Snd;

namespace Origo.Core.Abstractions;

/// <summary>
///     通用键值黑板接口，面向 Core 层的全局/进度/会话级共享状态。
///     内部使用 <see cref="TypedData" /> 保留类型信息，确保序列化/反序列化后类型不丢失。
/// </summary>
public interface IBlackboard
{
    void Set<T>(string key, T value);

    (bool found, T value) TryGet<T>(string key);

    T GetOrDefault<T>(string key, T defaultValue = default!);

    void Clear();

    IReadOnlyCollection<string> GetKeys();

    /// <summary>
    ///     导出全部条目（带类型信息），用于序列化持久化。
    /// </summary>
    IReadOnlyDictionary<string, TypedData> ExportAll();

    /// <summary>
    ///     从带类型信息的字典恢复全部条目，替换当前内容。
    /// </summary>
    void ImportAll(IReadOnlyDictionary<string, TypedData> data);
}