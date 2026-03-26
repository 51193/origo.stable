using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.StateMachine;

/// <summary>
///     字符串栈状态机：Push 调用 Push 策略的 <see cref="BaseSndStrategy.AfterAdd" />，
///     Pop 在出栈前调用 Pop 策略的 <see cref="BaseSndStrategy.BeforeQuit" />。
/// </summary>
public sealed class StackStateMachine : IStateMachine, IDisposable
{
    private readonly SndContext _ctx;
    private readonly SndStrategyPool _pool;
    private readonly BaseSndStrategy _popStrategy;
    private readonly BaseSndStrategy _pushStrategy;
    private readonly List<string> _stack = new();
    private bool _disposed;

    internal StackStateMachine(
        string machineKey,
        string pushStrategyIndex,
        string popStrategyIndex,
        SndStrategyPool pool,
        SndContext ctx)
    {
        MachineKey = machineKey ?? throw new ArgumentNullException(nameof(machineKey));
        PushStrategyIndex = pushStrategyIndex ?? throw new ArgumentNullException(nameof(pushStrategyIndex));
        PopStrategyIndex = popStrategyIndex ?? throw new ArgumentNullException(nameof(popStrategyIndex));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

        _pushStrategy = _pool.GetStrategy(PushStrategyIndex)
                        ?? throw new InvalidOperationException($"Push strategy '{PushStrategyIndex}' not found.");
        _popStrategy = _pool.GetStrategy(PopStrategyIndex)
                       ?? throw new InvalidOperationException($"Pop strategy '{PopStrategyIndex}' not found.");
    }

    public void Dispose()
    {
        if (_disposed) return;

        _pool.ReleaseStrategy(PushStrategyIndex);
        _pool.ReleaseStrategy(PopStrategyIndex);
        _disposed = true;
    }

    public string MachineKey { get; }

    public string PushStrategyIndex { get; }

    public string PopStrategyIndex { get; }

    public void Push(string value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("State machine stack value cannot be null/empty.", nameof(value));

        var beforeTop = PeekTopOrNull();
        _stack.Add(value);
        var afterTop = PeekTopOrNull();

        var op = new StateMachineOperationContext(MachineKey, StateMachineDataKeys.OperationPush, beforeTop, afterTop);
        _pushStrategy.AfterAdd(new StateMachineStrategyEntityAdapter(op), _ctx);
    }

    public bool TryPopRuntime(out string? popped)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        popped = null;
        if (_stack.Count == 0) return false;

        var beforeTop = PeekTopOrNull();
        var willPop = _stack[^1];
        var afterTop = _stack.Count > 1 ? _stack[^2] : null;

        var op = new StateMachineOperationContext(MachineKey, StateMachineDataKeys.OperationPop, beforeTop, afterTop);
        _popStrategy.BeforeRemove(new StateMachineStrategyEntityAdapter(op), _ctx);

        _stack.RemoveAt(_stack.Count - 1);
        popped = willPop;
        return true;
    }

    public bool TryPopOnQuit(out string? popped)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        popped = null;
        if (_stack.Count == 0) return false;

        var beforeTop = PeekTopOrNull();
        var willPop = _stack[^1];
        var afterTop = _stack.Count > 1 ? _stack[^2] : null;

        var op = new StateMachineOperationContext(MachineKey, StateMachineDataKeys.OperationPop, beforeTop, afterTop);
        _popStrategy.BeforeQuit(new StateMachineStrategyEntityAdapter(op), _ctx);

        _stack.RemoveAt(_stack.Count - 1);
        popped = willPop;
        return true;
    }

    public (bool found, string? top) Peek()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stack.Count == 0) return (false, null);
        return (true, _stack[^1]);
    }

    public IReadOnlyList<string> Snapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _stack.ToArray();
    }

    public void RestoreStackWithoutHooks(IReadOnlyList<string> stackBottomToTop)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(stackBottomToTop);

        _stack.Clear();
        foreach (var v in stackBottomToTop)
        {
            if (string.IsNullOrWhiteSpace(v))
                throw new InvalidOperationException("State machine snapshot contains null/empty value.");
            _stack.Add(v);
        }
    }

    public void FlushAfterLoad()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        for (var i = 0; i < _stack.Count; i++)
        {
            var beforeTop = i == 0 ? null : _stack[i - 1];
            var afterTop = _stack[i];
            var op = new StateMachineOperationContext(MachineKey, StateMachineDataKeys.OperationAfterLoad, beforeTop,
                afterTop);
            _pushStrategy.AfterLoad(new StateMachineStrategyEntityAdapter(op), _ctx);
        }
    }

    private string? PeekTopOrNull()
    {
        return _stack.Count == 0 ? null : _stack[^1];
    }
}