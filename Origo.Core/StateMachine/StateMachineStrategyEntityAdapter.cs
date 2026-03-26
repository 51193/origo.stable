using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;

namespace Origo.Core.StateMachine;

/// <summary>
///     仅供字符串栈状态机在调用 <see cref="Snd.Strategy.BaseSndStrategy" /> 钩子时作为 <see cref="ISndEntity" /> 传入。
///     仅支持 <see cref="GetData{T}" />，其余成员会抛异常。
/// </summary>
public sealed class StateMachineStrategyEntityAdapter : ISndEntity
{
    private readonly StateMachineOperationContext _context;

    public StateMachineStrategyEntityAdapter(StateMachineOperationContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public string Name => "<StateMachine>";

    public T GetData<T>(string name)
    {
        if (name == StateMachineDataKeys.BeforeTop)
        {
            if (typeof(T) != typeof(string))
                throw new InvalidOperationException(
                    $"State machine data '{StateMachineDataKeys.BeforeTop}' is a nullable string; requested '{typeof(T).Name}'.");
            return (T)(object?)_context.BeforeTop!;
        }

        if (name == StateMachineDataKeys.AfterTop)
        {
            if (typeof(T) != typeof(string))
                throw new InvalidOperationException(
                    $"State machine data '{StateMachineDataKeys.AfterTop}' is a nullable string; requested '{typeof(T).Name}'.");
            return (T)(object?)_context.AfterTop!;
        }

        if (name == StateMachineDataKeys.Operation) return (T)(object)_context.Operation;

        if (name == StateMachineDataKeys.MachineKey) return (T)(object)_context.MachineKey;

        throw new KeyNotFoundException($"Unknown state machine data key '{name}'.");
    }

    public (bool found, T value) TryGetData<T>(string name)
    {
        if (name == StateMachineDataKeys.BeforeTop)
        {
            if (typeof(T) != typeof(string))
                return (false, default!);
            if (_context.BeforeTop == null)
                return (false, default!);
            return (true, (T)(object)_context.BeforeTop);
        }

        if (name == StateMachineDataKeys.AfterTop)
        {
            if (typeof(T) != typeof(string))
                return (false, default!);
            if (_context.AfterTop == null)
                return (false, default!);
            return (true, (T)(object)_context.AfterTop);
        }

        try
        {
            var v = GetData<T>(name);
            return (true, v);
        }
        catch (KeyNotFoundException)
        {
            return (false, default!);
        }
    }

    public void SetData<T>(string name, T value)
    {
        throw NotSupported();
    }

    public void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter = null)
    {
        throw NotSupported();
    }

    public void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback)
    {
        throw NotSupported();
    }

    public INodeHandle? GetNode(string name)
    {
        throw NotSupported();
    }

    public IReadOnlyCollection<string> GetNodeNames()
    {
        throw NotSupported();
    }

    public void AddStrategy(string index)
    {
        throw NotSupported();
    }

    public void RemoveStrategy(string index)
    {
        throw NotSupported();
    }

    private static NotSupportedException NotSupported()
    {
        return new NotSupportedException("StateMachine strategy adapter supports GetData only.");
    }
}

/// <summary>
///     单次 Push/Pop/AfterLoad 刷新时的栈顶前后快照。
/// </summary>
public sealed class StateMachineOperationContext
{
    public StateMachineOperationContext(string machineKey, string operation, string? beforeTop, string? afterTop)
    {
        MachineKey = machineKey ?? throw new ArgumentNullException(nameof(machineKey));
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        BeforeTop = beforeTop;
        AfterTop = afterTop;
    }

    public string MachineKey { get; }

    public string Operation { get; }

    public string? BeforeTop { get; }

    public string? AfterTop { get; }
}