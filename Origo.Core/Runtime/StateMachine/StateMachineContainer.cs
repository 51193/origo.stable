using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;

namespace Origo.Core.Runtime.StateMachine;

/// <summary>
///     按字符串 key 管理多个 <see cref="StackStateMachine" />，生命周期与策略池引用计数对齐。
/// </summary>
public sealed class StateMachineContainer
{
    private readonly SndContext _ctx;
    private readonly List<string> _machineOrder = new();
    private readonly Dictionary<string, StackStateMachine> _machines = new(StringComparer.Ordinal);
    private readonly SndStrategyPool _pool;

    internal StateMachineContainer(SndStrategyPool pool, SndContext ctx)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    }

    public StackStateMachine CreateOrGet(string machineKey, string pushStrategyIndex, string popStrategyIndex)
    {
        if (string.IsNullOrWhiteSpace(machineKey))
            throw new ArgumentException("Machine key cannot be null or whitespace.", nameof(machineKey));
        if (string.IsNullOrWhiteSpace(pushStrategyIndex))
            throw new ArgumentException("Push strategy index cannot be null or whitespace.",
                nameof(pushStrategyIndex));
        if (string.IsNullOrWhiteSpace(popStrategyIndex))
            throw new ArgumentException("Pop strategy index cannot be null or whitespace.", nameof(popStrategyIndex));

        if (_machines.TryGetValue(machineKey, out var existing))
        {
            if (existing.PushStrategyIndex != pushStrategyIndex || existing.PopStrategyIndex != popStrategyIndex)
                throw new InvalidOperationException(
                    $"State machine '{machineKey}' already exists with different strategy indices.");

            return existing;
        }

        var sm = new StackStateMachine(machineKey, pushStrategyIndex, popStrategyIndex, _pool, _ctx);
        _machines[machineKey] = sm;
        _machineOrder.Add(machineKey);
        return sm;
    }

    public bool TryGet(string machineKey, out StackStateMachine? machine)
    {
        return _machines.TryGetValue(machineKey, out machine);
    }

    public void Remove(string machineKey)
    {
        if (!_machines.TryGetValue(machineKey, out var sm)) return;
        sm.Dispose();
        _machines.Remove(machineKey);
        _machineOrder.Remove(machineKey);
    }

    public void Clear()
    {
        foreach (var sm in _machines.Values)
            sm.Dispose();

        _machines.Clear();
        _machineOrder.Clear();
    }

    public void FlushAllAfterLoad()
    {
        foreach (var sm in EnumerateMachinesInInsertionOrder())
            sm.FlushAfterLoad();
    }

    public void PopAllRuntime()
    {
        foreach (var sm in EnumerateMachinesInInsertionOrder())
            while (sm.TryPopRuntime(out _))
            {
                // Let-it-crash: any hook/state error should surface immediately.
            }
    }

    public void PopAllOnQuit()
    {
        foreach (var sm in EnumerateMachinesInInsertionOrder())
            while (sm.TryPopOnQuit(out _))
            {
                // Let-it-crash: any hook/state error should surface immediately.
            }
    }

    public string ExportToJson(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var payload = new StateMachineContainerPayload();
        foreach (var key in _machineOrder)
        {
            if (!_machines.TryGetValue(key, out var sm))
                throw new InvalidOperationException($"StateMachineContainer order contains missing key '{key}'.");
            payload.Machines.Add(new StateMachineEntryPayload
            {
                Key = key,
                PushIndex = sm.PushStrategyIndex,
                PopIndex = sm.PopStrategyIndex,
                Stack = sm.Snapshot().ToList()
            });
        }

        return JsonSerializer.Serialize(payload, options);
    }

    public void ImportWithoutHooks(string json, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("StateMachineContainer json cannot be null/empty.");

        var payload = JsonSerializer.Deserialize<StateMachineContainerPayload>(json, options);
        if (payload?.Machines == null)
            throw new InvalidOperationException("StateMachineContainer payload.machines is required.");

        // Build new state first; only replace after full validation succeeds.
        var newOrder = new List<string>(payload.Machines.Count);
        var newMachines = new Dictionary<string, StackStateMachine>(payload.Machines.Count, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            foreach (var entry in payload.Machines)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    throw new InvalidOperationException("StateMachineEntry key is required.");
                if (!seen.Add(entry.Key))
                    throw new InvalidOperationException($"Duplicate state machine key '{entry.Key}' in payload.");
                if (string.IsNullOrWhiteSpace(entry.PushIndex))
                    throw new InvalidOperationException($"StateMachineEntry '{entry.Key}' missing push index.");
                if (string.IsNullOrWhiteSpace(entry.PopIndex))
                    throw new InvalidOperationException($"StateMachineEntry '{entry.Key}' missing pop index.");
                if (entry.Stack == null)
                    throw new InvalidOperationException($"StateMachineEntry '{entry.Key}' stack is required.");

                // Constructing StackStateMachine acquires strategies from the pool; dispose on any failure.
                var sm = new StackStateMachine(entry.Key, entry.PushIndex, entry.PopIndex, _pool, _ctx);
                sm.RestoreStackWithoutHooks(entry.Stack);
                newMachines[entry.Key] = sm;
                newOrder.Add(entry.Key);
            }
        }
        catch
        {
            foreach (var sm in newMachines.Values)
                sm.Dispose();
            throw;
        }

        Clear();
        foreach (var key in newOrder)
        {
            _machineOrder.Add(key);
            _machines[key] = newMachines[key];
        }
    }

    private IEnumerable<StackStateMachine> EnumerateMachinesInInsertionOrder()
    {
        foreach (var key in _machineOrder)
        {
            if (!_machines.TryGetValue(key, out var sm))
                throw new InvalidOperationException($"StateMachineContainer order contains missing key '{key}'.");
            yield return sm;
        }
    }
}