using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Serialization;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     负责 SystemRun / ProgressRun / SessionRun 的依赖注入与构造。
///     实际依赖由 <see cref="RunDependencies" /> 持有，本类仅作为薄创建门面。
/// </summary>
internal sealed class RunFactory
{
    public RunFactory(
        ILogger logger,
        IFileSystem fileSystem,
        string saveRootPath,
        OrigoRuntime runtime,
        IStateMachineContext? stateMachineContext = null,
        ISndContext? sndContext = null,
        ISaveStorageService? storageService = null,
        ISavePathPolicy? savePathPolicy = null)
    {
        Dependencies = new RunDependencies(logger, fileSystem, saveRootPath, runtime,
            stateMachineContext, sndContext, storageService, savePathPolicy);
    }

    internal RunDependencies Dependencies { get; }

    public ILogger Logger => Dependencies.Logger;

    public IFileSystem FileSystem => Dependencies.FileSystem;

    public string SaveRootPath => Dependencies.SaveRootPath;

    public OrigoRuntime Runtime => Dependencies.Runtime;

    /// <summary>
    ///     创建流程/会话时用于字符串栈状态机与策略池回调；由 <see cref="SndContext" /> 注入。
    /// </summary>
    public IStateMachineContext? StateMachineContext => Dependencies.StateMachineContext;

    /// <summary>
    ///     SND 上下文接口，用于后台会话的场景宿主绑定等操作。
    /// </summary>
    public ISndContext? SndContext => Dependencies.SndContext;

    /// <summary>
    ///     存档读写服务实例。
    /// </summary>
    public ISaveStorageService StorageService => Dependencies.StorageService;

    /// <summary>
    ///     存档路径策略。
    /// </summary>
    public ISavePathPolicy SavePathPolicy => Dependencies.SavePathPolicy;

    public SystemRun CreateSystemRun() => new(this);

    public ProgressRun CreateProgressRun(
        string saveId,
        IBlackboard progressBlackboard)
    {
        if (StateMachineContext is null)
            throw new InvalidOperationException(
                "StateMachineContext must be set on RunFactory before creating ProgressRun.");

        ArgumentNullException.ThrowIfNull(progressBlackboard);
        var progressMachines = new StateMachineContainer(Runtime.SndWorld.StrategyPool, StateMachineContext);
        var scope = new RunStateScope(progressBlackboard, progressMachines);
        return new ProgressRun(this, scope, saveId);
    }

    public ISessionRun CreateSessionRun(
        SaveContext saveContext,
        string levelId,
        IBlackboard sessionBlackboard,
        ISndSceneHost sceneHost)
    {
        if (StateMachineContext is null)
            throw new InvalidOperationException(
                "StateMachineContext must be set on RunFactory before creating SessionRun.");

        var sessionCtx = new SessionStateMachineContext(StateMachineContext, sessionBlackboard, sceneHost);
        var sessionMachines = new StateMachineContainer(Runtime.SndWorld.StrategyPool, sessionCtx);
        return new SessionRun(
            saveContext,
            levelId,
            sessionBlackboard,
            sceneHost,
            sessionMachines,
            StorageService,
            Logger);
    }

    /// <summary>
    ///     创建一个后台关卡会话（<see cref="ISessionRun" />）。
    ///     使用 <see cref="FullMemorySndSceneHost" /> 和 <see cref="NullNodeFactory" />，
    ///     共享当前状态机上下文的策略池、进度黑板与文件系统，
    ///     但拥有独立的 SessionBlackboard、状态机和实体集合。
    /// </summary>
    /// <param name="levelId">后台关卡标识符。</param>
    /// <returns>
    ///     与前台关卡同一接口的 <see cref="ISessionRun" />，仅注入了内存场景宿主而非引擎适配层宿主。
    /// </returns>
    public ISessionRun CreateBackgroundSession(string levelId)
    {
        var sessionRun = CreateBackgroundSessionCore(levelId);

        // Flush state machines to ensure consistent initial state.
        sessionRun.GetSessionStateMachines().FlushAllAfterLoad();

        return sessionRun;
    }

    /// <summary>
    ///     创建一个后台关卡会话并从 <see cref="LevelPayload" /> 恢复状态。
    ///     等价于 <see cref="CreateBackgroundSession" /> + <see cref="ISessionRun.LoadFromPayload" />，
    ///     适用于从存档数据初始化后台关卡（如后台 AI 仿真加载已有关卡数据）。
    /// </summary>
    /// <param name="levelId">后台关卡标识符。</param>
    /// <param name="payload">要恢复的关卡数据。</param>
    public ISessionRun CreateBackgroundSessionFromPayload(string levelId, LevelPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var sessionRun = CreateBackgroundSessionCore(levelId);

        // 从 payload 恢复会话状态（内部包含 FlushAllAfterLoad）。
        ((SessionRun)sessionRun).LoadFromPayload(payload);

        return sessionRun;
    }

    /// <summary>
    ///     后台会话创建的共享核心逻辑：校验前置条件、构建 FullMemorySndSceneHost 并组装 SessionRun。
    ///     不触发 FlushAllAfterLoad，由调用方按需决定刷新时机。
    /// </summary>
    private ISessionRun CreateBackgroundSessionCore(string levelId)
    {
        if (StateMachineContext is null)
            throw new InvalidOperationException(
                "StateMachineContext must be set on RunFactory before creating a background session.");
        if (SndContext is null)
            throw new InvalidOperationException(
                "SndContext must be set on RunFactory before creating a background session.");
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));

        var sceneHost = new FullMemorySndSceneHost(Logger);
        sceneHost.BindWorld(Runtime.SndWorld);
        sceneHost.BindContext(SndContext);

        var sessionBlackboard = new Blackboard.Blackboard();
        var progressBb = StateMachineContext.ProgressBlackboard ?? new Blackboard.Blackboard();
        var saveContext = CreateSaveContext(progressBb, sessionBlackboard);

        return CreateSessionRun(saveContext, levelId, sessionBlackboard, sceneHost);
    }

    public SaveContext CreateSaveContext(IBlackboard progressBlackboard, IBlackboard sessionBlackboard)
    {
        ArgumentNullException.ThrowIfNull(progressBlackboard);
        ArgumentNullException.ThrowIfNull(sessionBlackboard);
        return new SaveContext(progressBlackboard, sessionBlackboard, Runtime.SndWorld);
    }
}
