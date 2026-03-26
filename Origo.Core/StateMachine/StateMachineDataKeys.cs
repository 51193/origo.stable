namespace Origo.Core.StateMachine;

/// <summary>
///     状态机策略在 <see cref="Abstractions.ISndEntity.GetData{T}" /> 中使用的约定键。
/// </summary>
public static class StateMachineDataKeys
{
    public const string BeforeTop = "sm.beforeTop";

    public const string AfterTop = "sm.afterTop";

    /// <summary>
    ///     操作类型：<c>push</c>、<c>pop</c>、<c>afterload</c>。
    /// </summary>
    public const string Operation = "sm.operation";

    public const string MachineKey = "sm.machineKey";

    public const string OperationPush = "push";

    public const string OperationPop = "pop";

    public const string OperationAfterLoad = "afterload";
}