using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Runtime.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     流程级运行时的只读门面接口。
///     外部代码（策略层）仅通过此接口访问流程状态；所有生命周期操作（创建会话、加载存档、持久化等）
///     由具体实现 <see cref="ProgressRun" /> 封装，外部不得直接调用。
///     <para>
///         设计原则：对外仅暴露一个 Run 的引用即可——策略只需读取黑板、通过 SessionManager 访问会话和状态机容器。
///         架构上不区分前台/后台会话；前台会话只是一个以 <see cref="ISessionManager.ForegroundKey" />
///         为键挂载在 SessionManager 上的普通会话，唯一的特殊之处在于至多一个。
///     </para>
/// </summary>
public interface IProgressRun : IDisposable
{
    /// <summary>流程级黑板。</summary>
    IBlackboard ProgressBlackboard { get; }

    /// <summary>
    ///     会话管理器，以 KVP 形式统一管理所有挂载的 <see cref="ISessionRun" />。
    ///     前台会话以 <see cref="ISessionManager.ForegroundKey" /> 为键挂载，与后台会话无架构区别。
    /// </summary>
    ISessionManager SessionManager { get; }

    string SaveId { get; }

    /// <summary>
    ///     流程级字符串栈状态机容器。策略层可通过此方法创建/获取流程级状态机。
    /// </summary>
    StateMachineContainer GetProgressStateMachines();
}
