using Origo.Core.Runtime;

namespace Origo.Core.Abstractions;

/// <summary>
///     提供 OrigoRuntime 实例的宿主抽象。
///     典型实现由具体引擎适配层（如 GodotAdapter）提供。
/// </summary>
public interface IOrigoRuntimeProvider
{
    OrigoRuntime Runtime { get; }
}