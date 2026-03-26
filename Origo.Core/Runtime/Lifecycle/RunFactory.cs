using System;
using Origo.Core.Abstractions;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     负责 SystemRun / ProgressRun / SessionRun 的依赖注入与构造。
/// </summary>
public sealed class RunFactory
{
    public RunFactory(
        ILogger logger,
        IFileSystem fileSystem,
        string saveRootPath,
        OrigoRuntime runtime,
        SndContext? sndContext = null)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        SaveRootPath = saveRootPath ?? throw new ArgumentNullException(nameof(saveRootPath));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        SndContext = sndContext;
    }

    public ILogger Logger { get; }

    public IFileSystem FileSystem { get; }

    public string SaveRootPath { get; }

    public OrigoRuntime Runtime { get; }

    /// <summary>
    ///     创建流程/会话时用于字符串栈状态机与策略池回调；由 <see cref="SndContext" /> 注入。
    /// </summary>
    public SndContext? SndContext { get; }

    public SystemRun CreateSystemRun()
    {
        return new SystemRun(this);
    }

    public ProgressRun CreateProgressRun(
        string saveId,
        string activeLevelId,
        IBlackboard? progressBlackboard = null)
    {
        if (SndContext == null)
            throw new InvalidOperationException("SndContext must be set on RunFactory before creating ProgressRun.");

        var bb = progressBlackboard ?? new Blackboard.Blackboard();
        var progressMachines = new StateMachineContainer(Runtime.SndWorld.StrategyPool, SndContext);
        var scope = new RunStateScope(bb, progressMachines);
        return new ProgressRun(this, scope, saveId, activeLevelId);
    }

    public SessionRun CreateSessionRun(
        SaveContext saveContext,
        string levelId,
        IBlackboard sessionBlackboard,
        ISndSceneAccess sceneAccess)
    {
        if (SndContext == null)
            throw new InvalidOperationException("SndContext must be set on RunFactory before creating SessionRun.");

        var sessionMachines = new StateMachineContainer(Runtime.SndWorld.StrategyPool, SndContext);
        return new SessionRun(
            saveContext,
            levelId,
            sessionBlackboard,
            sceneAccess,
            sessionMachines,
            FileSystem,
            SaveRootPath);
    }

    public SaveContext CreateSaveContext(IBlackboard progressBlackboard, IBlackboard sessionBlackboard)
    {
        ArgumentNullException.ThrowIfNull(progressBlackboard);
        ArgumentNullException.ThrowIfNull(sessionBlackboard);
        return new SaveContext(progressBlackboard, sessionBlackboard, Runtime.SndWorld);
    }
}