using System;
using Origo.Core.Abstractions;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     负责 SystemRun / ProgressRun / SessionRun 的依赖注入与构造。
///     实际依赖由 <see cref="RunDependencies" /> 持有，本类仅作为薄创建门面。
/// </summary>
public sealed class RunFactory
{
    internal RunDependencies Dependencies { get; }

    public RunFactory(
        ILogger logger,
        IFileSystem fileSystem,
        string saveRootPath,
        OrigoRuntime runtime,
        SndContext? sndContext = null)
    {
        Dependencies = new RunDependencies(logger, fileSystem, saveRootPath, runtime, sndContext);
    }

    public ILogger Logger => Dependencies.Logger;

    public IFileSystem FileSystem => Dependencies.FileSystem;

    public string SaveRootPath => Dependencies.SaveRootPath;

    public OrigoRuntime Runtime => Dependencies.Runtime;

    /// <summary>
    ///     创建流程/会话时用于字符串栈状态机与策略池回调；由 <see cref="SndContext" /> 注入。
    /// </summary>
    public SndContext? SndContext => Dependencies.SndContext;

    public SystemRun CreateSystemRun()
    {
        return new SystemRun(this);
    }

    public ProgressRun CreateProgressRun(
        string saveId,
        string activeLevelId,
        IBlackboard progressBlackboard)
    {
        if (SndContext is null)
            throw new InvalidOperationException("SndContext must be set on RunFactory before creating ProgressRun.");

        ArgumentNullException.ThrowIfNull(progressBlackboard);
        var progressMachines = new StateMachineContainer(Runtime.SndWorld.StrategyPool, SndContext);
        var scope = new RunStateScope(progressBlackboard, progressMachines);
        return new ProgressRun(this, scope, saveId, activeLevelId);
    }

    public SessionRun CreateSessionRun(
        SaveContext saveContext,
        string levelId,
        IBlackboard sessionBlackboard,
        ISndSceneAccess sceneAccess)
    {
        if (SndContext is null)
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