using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.StateMachine;

/// <summary>
///     字符串栈状态机绑定的策略基类。钩子命名表示「操作 + 时机」，与 <see cref="StackStateMachine" /> 的调度点一一对应。
///     钩子参数为 <see cref="IStateMachineContext" /> 接口，前后台共用同一抽象，禁止按实现类型分叉。
/// </summary>
public abstract class StateMachineStrategyBase : BaseStrategy
{
    /// <summary>运行时 <see cref="StackStateMachine.Push" /> 入栈成功后调用。</summary>
    public virtual void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }

    /// <summary>读档恢复栈后，按从栈底到栈顶顺序对每层调用（见 <see cref="StackStateMachine.FlushAfterLoad" />）。</summary>
    public virtual void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }

    /// <summary>运行时 <see cref="StackStateMachine.TryPopRuntime" /> 出栈前调用。</summary>
    public virtual void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }

    /// <summary>退出流程 <see cref="StackStateMachine.TryPopOnQuit" /> 出栈前调用。</summary>
    public virtual void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
    }
}
