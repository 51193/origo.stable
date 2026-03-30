using System;
using System.Text.Json;
using Origo.Core.Abstractions;
using Origo.Core.Runtime.Console;
using Origo.Core.Scheduling;
using Origo.Core.Serialization;
using Origo.Core.Snd;

namespace Origo.Core.Runtime;

/// <summary>
///     Origo 在宿主游戏中的统一运行时入口。
///     聚合 SND 子系统与系统级黑板。
///     <para>
///         线程模型：未做跨线程同步；<see cref="EnqueueBusinessDeferred" /> 与 <see cref="EnqueueSystemDeferred" />
///         应在宿主主线程（或单线程游戏主循环）上调用，与 <see cref="FlushEndOfFrameDeferred" /> 成对使用。
///     </para>
/// </summary>
public sealed class OrigoRuntime
{
    private readonly ActionScheduler _businessDeferredScheduler;
    private readonly ActionScheduler _systemDeferredScheduler;

    public OrigoRuntime(
        ILogger logger,
        ISndSceneHost sndSceneHost,
        TypeStringMapping typeStringMapping,
        Action<JsonSerializerOptions>? configureJson = null,
        IBlackboard? systemBlackboard = null,
        ConsoleInputQueue? consoleInput = null,
        IConsoleOutputChannel? consoleOutputChannel = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
        ArgumentNullException.ThrowIfNull(sndSceneHost);
        ArgumentNullException.ThrowIfNull(typeStringMapping);
        SndWorld = new SndWorld(typeStringMapping, Logger, configureJson);
        Snd = new SndRuntime(SndWorld, sndSceneHost);
        _businessDeferredScheduler = new ActionScheduler(Logger);
        _systemDeferredScheduler = new ActionScheduler(Logger);

        ArgumentNullException.ThrowIfNull(systemBlackboard);
        SystemBlackboard = systemBlackboard;

        ConsoleInput = consoleInput;
        ConsoleOutputChannel = consoleOutputChannel;
        if (consoleInput is not null && consoleOutputChannel is not null)
            Console = new OrigoConsole(consoleInput, consoleOutputChannel, this);
    }

    public ILogger Logger { get; }

    public SndWorld SndWorld { get; }

    public SndRuntime Snd { get; }

    public IBlackboard SystemBlackboard { get; }

    /// <summary>
    ///     若启动时同时注入了输入队列与输出通道，则为非 null。
    /// </summary>
    public ConsoleInputQueue? ConsoleInput { get; }

    public IConsoleOutputChannel? ConsoleOutputChannel { get; }

    public OrigoConsole? Console { get; }

    public void EnqueueBusinessDeferred(Action action)
    {
        _businessDeferredScheduler.Enqueue(action);
    }

    public void EnqueueSystemDeferred(Action action)
    {
        _systemDeferredScheduler.Enqueue(action);
    }

    public void FlushEndOfFrameDeferred()
    {
        _businessDeferredScheduler.Tick();
        _systemDeferredScheduler.Tick();
    }

    /// <summary>
    ///     重置控制台状态：清空待执行输入队列。
    ///     输出已改为发布订阅模型，不在 Core 中保留历史。
    /// </summary>
    public void ResetConsoleState()
    {
        ConsoleInput?.Clear();
    }
}