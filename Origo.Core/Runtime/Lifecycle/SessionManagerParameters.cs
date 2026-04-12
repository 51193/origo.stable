namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     SessionManager 构造所需的配置参数。
///     当前为空——所有运行时依赖均通过 <see cref="ProgressRuntime" /> 传递，
///     此结构体保留以满足统一的 (Runtime, Parameters) 构造模式。
/// </summary>
internal readonly record struct SessionManagerParameters;
