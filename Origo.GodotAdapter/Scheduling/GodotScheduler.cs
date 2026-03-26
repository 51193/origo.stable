using System;
using Origo.Core.Abstractions;
using Origo.Core.Scheduling;

namespace Origo.GodotAdapter.Scheduling;

/// <summary>
///     针对 Godot 帧循环的调度适配器。
///     具体的 Godot Node 壳层由宿主 Godot 项目实现并在 _Process 中调用 Tick。
/// </summary>
public sealed class GodotScheduler : IScheduler
{
    private readonly ActionScheduler _scheduler;

    public GodotScheduler(ILogger? logger = null)
    {
        _scheduler = new ActionScheduler(logger);
    }

    public void Enqueue(Action action)
    {
        _scheduler.Enqueue(action);
    }

    public void Tick()
    {
        _scheduler.Tick();
    }
}