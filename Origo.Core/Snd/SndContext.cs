using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Origo.Core.Abstractions;
using Origo.Core.Logging;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;

namespace Origo.Core.Snd;

/// <summary>
///     面向策略与游戏层的统一生命周期编排门面。
///     对外暴露的存档、继续游戏、切换关卡 API，统一编排到三层运行实例：
///     <list type="bullet">
///         <item>
///             <description>SystemRun：系统级实例，维护 continue/active slot/save 等全局索引。</description>
///         </item>
///         <item>
///             <description>ProgressRun：流程级实例，维护 ProgressBlackboard 与 ActiveLevelId。</description>
///         </item>
///         <item>
///             <description>SessionRun：会话级实例，维护 SessionBlackboard 与当前 SND 场景。</description>
///         </item>
///     </list>
///     该类不再直接持有流程/会话黑板实例，避免实例生命周期与逻辑生命周期不一致。
///     在尚未建立流程（<see cref="IProgressRun" />）时，<see cref="ProgressBlackboard" /> 与
///     <see cref="SessionBlackboard" /> 为 null；不得在无流程时向"空黑板替身"写入。
/// </summary>
public sealed partial class SndContext
{
    private readonly List<ISaveMetaContributor> _saveMetaContributors = new();
    private int _pendingPersistenceRequests;
    private readonly SystemRun _systemRun;
    private IProgressRun? _progressRun;
    /// <summary>
    ///     Guard flag to prevent concurrent lifecycle workflows.
    ///     Only accessed from deferred queue callbacks which execute sequentially on the game loop thread,
    ///     so no synchronization is required.
    /// </summary>
    private bool _workflowInProgress;

    private readonly SaveGameWorkflow _saveGameWorkflow;
    private readonly EntryPointWorkflow _entryPointWorkflow;

    public SndContext(
        OrigoRuntime runtime,
        IFileSystem fileSystem,
        string saveRootPath,
        string initialSaveRootPath,
        string entryConfigPath)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(fileSystem);
        Runtime = runtime;
        FileSystem = fileSystem;

        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));
        if (string.IsNullOrWhiteSpace(initialSaveRootPath))
            throw new ArgumentException("Initial save root path cannot be null or whitespace.",
                nameof(initialSaveRootPath));
        if (string.IsNullOrWhiteSpace(entryConfigPath))
            throw new ArgumentException("Entry config path cannot be null or whitespace.", nameof(entryConfigPath));

        SaveRootPath = saveRootPath;
        InitialSaveRootPath = initialSaveRootPath;
        EntryConfigPath = entryConfigPath;

        RunFactory = new RunFactory(Runtime.Logger, FileSystem, SaveRootPath, Runtime, this);
        _systemRun = RunFactory.CreateSystemRun();

        _saveGameWorkflow = new SaveGameWorkflow(this);
        _entryPointWorkflow = new EntryPointWorkflow(this);
    }

    internal OrigoRuntime Runtime { get; }

    internal IFileSystem FileSystem { get; }

    public string SaveRootPath { get; }

    public string InitialSaveRootPath { get; }
    public string EntryConfigPath { get; }

    internal RunFactory RunFactory { get; }

    internal IReadOnlyList<ISaveMetaContributor> SaveMetaContributors => _saveMetaContributors;

    public IBlackboard SystemBlackboard => _systemRun.SystemBlackboard;

    /// <summary>
    ///     当前流程级黑板；无活动流程时为 null。
    /// </summary>
    public IBlackboard? ProgressBlackboard => _progressRun?.ProgressBlackboard;

    /// <summary>
    ///     当前关卡会话黑板；无当前会话时为 null。
    /// </summary>
    public IBlackboard? SessionBlackboard => _progressRun?.CurrentSession?.SessionBlackboard;
    public SndRuntime SndRuntime => Runtime.Snd;

    private JsonSerializerOptions JsonOptions => Runtime.SndWorld.JsonOptions;

    // ── Helpers shared with workflows ──────────────────────────────────

    internal string? TryGetActiveSaveId()
    {
        var (found, value) = SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        return found && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    internal void SetActiveSaveState(string saveId)
    {
        _systemRun.SetActiveSaveSlot(saveId);
    }

    internal IProgressRun EnsureProgressRun()
    {
        return _progressRun ?? throw new InvalidOperationException(
            "No active ProgressRun. Call RequestLoadGame/RequestContinueGame/RequestLoadInitialSave/RequestLoadMainMenuEntrySave first.");
    }

    internal (IProgressRun progressRun, ISessionRun sessionRun) EnsureProgressAndSession()
    {
        var progressRun = EnsureProgressRun();
        var sessionRun = progressRun.CurrentSession ?? throw new InvalidOperationException(
            "No active SessionRun. Current progress run has not created a session instance.");
        return (progressRun, sessionRun);
    }

    internal void SetProgressRun(IProgressRun? progressRun)
    {
        _progressRun = progressRun;
    }

    /// <summary>
    ///     标记工作流开始。若已有工作流正在执行则抛出，防止并发工作流导致 <see cref="_progressRun" /> 竞态。
    /// </summary>
    internal void BeginWorkflow()
    {
        if (_workflowInProgress)
            throw new InvalidOperationException(
                "A lifecycle workflow (load/save/change-level) is already in progress. " +
                "Concurrent workflow operations are not supported.");
        _workflowInProgress = true;
    }

    /// <summary>
    ///     标记工作流完成。
    /// </summary>
    internal void EndWorkflow()
    {
        _workflowInProgress = false;
    }

    internal void IncrementPendingPersistence() => Interlocked.Increment(ref _pendingPersistenceRequests);
    internal void DecrementPendingPersistence() => Interlocked.Decrement(ref _pendingPersistenceRequests);

    internal void ShutdownCurrentProgressAndScene()
    {
        _progressRun?.Dispose();
        _progressRun = null;
        Runtime.Snd.ClearAll();
    }

    // ── Public API ─────────────────────────────────────────────────────

    public void EnqueueBusinessDeferred(Action action)
    {
        Runtime.EnqueueBusinessDeferred(action);
    }

    internal void EnqueueSystemDeferred(Action action)
    {
        Runtime.EnqueueSystemDeferred(action);
    }

    public int GetPendingPersistenceRequestCount()
    {
        return Interlocked.CompareExchange(ref _pendingPersistenceRequests, 0, 0);
    }

    public void FlushDeferredActionsForCurrentFrame()
    {
        Runtime.FlushEndOfFrameDeferred();
    }

    public void ClearAllSndEntities()
    {
        Runtime.Snd.ClearAll();
    }

    public void SpawnManySndEntities(IEnumerable<SndMetaData> metaList)
    {
        Runtime.Snd.SpawnMany(metaList);
    }

    public ISndEntity? FindSndEntity(string name)
    {
        return Runtime.Snd.FindByName(name);
    }

    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null)
    {
        var template = Runtime.SndWorld.ResolveTemplate(templateKey);
        var cloned = SndWorld.CloneMetaData(template);
        if (!string.IsNullOrWhiteSpace(overrideName))
            cloned.Name = overrideName;
        return cloned;
    }

    public bool TrySubmitConsoleCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return false;

        Runtime.ConsoleInput?.Enqueue(commandLine.Trim());
        return Runtime.ConsoleInput is not null;
    }

    public void ProcessConsolePending()
    {
        Runtime.Console?.ProcessPending();
    }

    public long SubscribeConsoleOutput(Action<string> onLine)
    {
        ArgumentNullException.ThrowIfNull(onLine);
        var channel = Runtime.ConsoleOutputChannel
                      ?? throw new InvalidOperationException("Console output channel is not available.");
        return channel.Subscribe(line => onLine(line ?? string.Empty));
    }

    public void UnsubscribeConsoleOutput(long subscriptionId)
    {
        if (subscriptionId <= 0)
            return;
        Runtime.ConsoleOutputChannel?.Unsubscribe(subscriptionId);
    }

    /// <summary>
    ///     流程级字符串栈状态机容器；无 <see cref="IProgressRun" /> 时为 null。
    /// </summary>
    public StateMachineContainer? GetProgressStateMachines()
    {
        return _progressRun?.ProgressScope.StateMachines;
    }

    /// <summary>
    ///     当前关卡会话级字符串栈状态机容器。
    /// </summary>
    public StateMachineContainer? GetSessionStateMachines()
    {
        return _progressRun?.CurrentSession?.SessionScope.StateMachines;
    }

    // ── Save flow delegation ───────────────────────────────────────────

    public IReadOnlyList<string> ListSaves() => _saveGameWorkflow.ListSaves();

    public IReadOnlyList<SaveMetaDataEntry> ListSavesWithMetaData() => _saveGameWorkflow.ListSavesWithMetaData();

    public void RequestSaveGame(
        string newSaveId,
        string baseSaveId,
        IReadOnlyDictionary<string, string>? customMeta = null)
        => _saveGameWorkflow.RequestSaveGame(newSaveId, baseSaveId, customMeta);

    public void RequestLoadGame(string saveId) => _saveGameWorkflow.RequestLoadGame(saveId);

    public bool RequestContinueGame() => _saveGameWorkflow.RequestContinueGame();

    /// <summary>
    ///     自动保存请求：
    ///     - baseSaveId 优先使用 SystemBlackboard 中的 active save id
    ///     - newSaveId 未指定时使用 Unix 毫秒时间戳
    /// </summary>
    public string RequestSaveGameAuto(
        string? newSaveId = null,
        IReadOnlyDictionary<string, string>? customMeta = null)
        => _saveGameWorkflow.RequestSaveGameAuto(newSaveId, customMeta);

    public bool HasContinueData() => _saveGameWorkflow.HasContinueData();

    public void SetContinueTarget(string saveId) => _saveGameWorkflow.SetContinueTarget(saveId);

    public void ClearContinueTarget() => _saveGameWorkflow.ClearContinueTarget();

    // ── Entry point delegation ─────────────────────────────────────────

    public void RequestLoadInitialSave() => _entryPointWorkflow.RequestLoadInitialSave();

    /// <summary>
    ///     按启动流程重新读取主菜单 entry 配置（与 OrigoDefaultEntry.ConfigPath 一致）。
    ///     不包含隐式保存；若业务需要保存，请先显式调用 RequestSaveGame/RequestSaveGameAuto。
    /// </summary>
    public void RequestLoadMainMenuEntrySave() => _entryPointWorkflow.RequestLoadMainMenuEntrySave();

    public void RequestChangeLevel(string newLevelId) => _entryPointWorkflow.RequestChangeLevel(newLevelId);
}
