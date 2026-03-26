using System;
using Origo.Core.Abstractions;
using Origo.Core.Runtime.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     将黑板与字符串栈状态机容器绑定在同一生命周期内。
/// </summary>
public sealed class RunStateScope
{
    public RunStateScope(IBlackboard blackboard, StateMachineContainer stateMachines)
    {
        Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
        StateMachines = stateMachines ?? throw new ArgumentNullException(nameof(stateMachines));
    }

    public IBlackboard Blackboard { get; }

    public StateMachineContainer StateMachines { get; }
}