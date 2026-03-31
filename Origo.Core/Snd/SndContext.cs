using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console.CommandImpl;
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
    private readonly EntryPointWorkflow _entryPointWorkflow;

    private readonly SaveGameWorkflow _saveGameWorkflow;
    private readonly List<ISaveMetaContributor> _saveMetaContributors = new();
    private readonly SystemRun _systemRun;
    private int _pendingPersistenceRequests;
    private IProgressRun? _progressRun;

    /// <summary>
    ///     Guard flag to prevent concurrent lifecycle workflows.
    ///     Only accessed from deferred queue callbacks which execute sequentially on the game loop thread,
    ///     so no synchronization is required.
    /// </summary>
    private bool _workflowInProgress;

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

        RegisterConsoleCommands();
    }

    /// <summary>
    ///     底层运行时实例。SndContext 通过 Runtime 访问 SndRuntime、SndWorld、黑板、控制台等子系统。
    ///     大部分公开 API 最终委托给 Runtime 中的对应方法。
    /// </summary>
    internal OrigoRuntime Runtime { get; }

    /// <summary>
    ///     文件系统抽象，用于存档读写操作。
    /// </summary>
    internal IFileSystem FileSystem { get; }

    /// <summary>
    ///     存档根路径，所有存档槽位和 current/ 目录均位于此路径下。
    /// </summary>
    public string SaveRootPath { get; }

    /// <summary>
    ///     初始存档模板路径，用于首次启动时从初始存档模板加载游戏。
    /// </summary>
    public string InitialSaveRootPath { get; }

    /// <summary>
    ///     入口配置文件路径，定义主菜单入口存档和启动流程配置。
    /// </summary>
    public string EntryConfigPath { get; }

    /// <summary>
    ///     运行实例工厂，负责创建 SystemRun / ProgressRun / SessionRun 三层运行实例。
    /// </summary>
    internal RunFactory RunFactory { get; }

    /// <summary>
    ///     已注册的存档元数据贡献者列表，存档时按顺序收集展示用 meta.map 条目。
    /// </summary>
    internal IReadOnlyList<ISaveMetaContributor> SaveMetaContributors => _saveMetaContributors;

    /// <summary>
    ///     系统级黑板（来自 SystemRun）。与 Runtime.SystemBlackboard 指向同一实例，此处提供快捷访问。
    /// </summary>
    public IBlackboard SystemBlackboard => _systemRun.SystemBlackboard;

    /// <summary>
    ///     当前流程级黑板（来自当前 ProgressRun）；无活动流程时为 null。
    /// </summary>
    public IBlackboard? ProgressBlackboard => _progressRun?.ProgressBlackboard;

    /// <summary>
    ///     当前关卡会话黑板（来自当前 SessionRun）；无活动会话时为 null。
    /// </summary>
    public IBlackboard? SessionBlackboard => _progressRun?.CurrentSession?.SessionBlackboard;

    /// <summary>
    ///     SND 运行时门面（来自 Runtime.Snd），提供 Spawn / 查询 / 序列化等操作。
    ///     此属性与 Runtime.Snd 是同一实例，此处提供快捷访问。
    /// </summary>
    public SndRuntime SndRuntime => Runtime.Snd;

    private void RegisterConsoleCommands()
    {
        var console = Runtime.Console;
        if (console is null)
            return;

        console.RegisterHandler(new ListSavesCommandHandler(this));
        console.RegisterHandler(new SaveGameCommandHandler(this));
        console.RegisterHandler(new LoadGameCommandHandler(this));
        console.RegisterHandler(new AutoSaveCommandHandler(this));
        console.RegisterHandler(new ContinueGameCommandHandler(this));
        console.RegisterHandler(new ChangeLevelCommandHandler(this));
    }

    // ── Helpers shared with workflows ──────────────────────────────────

    internal string? TryGetActiveSaveId()
    {
        var (found, value) = SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        return found && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    internal void SetActiveSaveState(string saveId) => _systemRun.SetActiveSaveSlot(saveId);

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

    internal void SetProgressRun(IProgressRun? progressRun) => _progressRun = progressRun;

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
    internal void EndWorkflow() => _workflowInProgress = false;

    internal void IncrementPendingPersistence() => Interlocked.Increment(ref _pendingPersistenceRequests);
    internal void DecrementPendingPersistence() => Interlocked.Decrement(ref _pendingPersistenceRequests);

    internal void ShutdownCurrentProgressAndScene()
    {
        _progressRun?.Dispose();
        _progressRun = null;
        Runtime.Snd.ClearAll();
    }

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>
    ///     将一个业务逻辑延迟动作加入队列，委托给 Runtime.EnqueueBusinessDeferred。
    /// </summary>
    public void EnqueueBusinessDeferred(Action action) => Runtime.EnqueueBusinessDeferred(action);

    /// <summary>
    ///     将一个系统级延迟动作加入队列，委托给 Runtime.EnqueueSystemDeferred。
    /// </summary>
    internal void EnqueueSystemDeferred(Action action) => Runtime.EnqueueSystemDeferred(action);

    /// <summary>
    ///     获取当前待执行的持久化请求计数。
    /// </summary>
    public int GetPendingPersistenceRequestCount() =>
        Interlocked.CompareExchange(ref _pendingPersistenceRequests, 0, 0);

    /// <summary>
    ///     执行当前帧的所有延迟动作，委托给 Runtime.FlushEndOfFrameDeferred。
    /// </summary>
    public void FlushDeferredActionsForCurrentFrame() => Runtime.FlushEndOfFrameDeferred();

    /// <summary>
    ///     清除所有 SND 实体，委托给 Runtime.Snd.ClearAll。
    /// </summary>
    public void ClearAllSndEntities() => Runtime.Snd.ClearAll();

    /// <summary>
    ///     批量生成多个 SND 实体，委托给 Runtime.Snd.SpawnMany。
    /// </summary>
    public void SpawnManySndEntities(IEnumerable<SndMetaData> metaList) => Runtime.Snd.SpawnMany(metaList);

    /// <summary>
    ///     按名称查找 SND 实体，委托给 Runtime.Snd.FindByName。
    /// </summary>
    public ISndEntity? FindSndEntity(string name) => Runtime.Snd.FindByName(name);

    /// <summary>
    ///     克隆指定模板并可选地覆盖名称，便于按模板批量创建实体。
    /// </summary>
    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null)
    {
        var template = Runtime.SndWorld.ResolveTemplate(templateKey);
        var cloned = SndWorld.CloneMetaData(template);
        if (!string.IsNullOrWhiteSpace(overrideName))
            cloned.Name = overrideName;
        return cloned;
    }

    /// <summary>
    ///     提交一条控制台命令到 Runtime.ConsoleInput 队列。若未注入输入队列则返回 false。
    /// </summary>
    public bool TrySubmitConsoleCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return false;

        Runtime.ConsoleInput?.Enqueue(commandLine.Trim());
        return Runtime.ConsoleInput is not null;
    }

    /// <summary>
    ///     处理控制台待执行命令，委托给 Runtime.Console.ProcessPending。
    /// </summary>
    public void ProcessConsolePending() => Runtime.Console?.ProcessPending();

    /// <summary>
    ///     订阅控制台输出，委托给 Runtime.ConsoleOutputChannel。返回订阅 ID，用于后续取消。
    /// </summary>
    public long SubscribeConsoleOutput(Action<string> onLine)
    {
        ArgumentNullException.ThrowIfNull(onLine);
        var channel = Runtime.ConsoleOutputChannel
                      ?? throw new InvalidOperationException("Console output channel is not available.");
        return channel.Subscribe(line => onLine(line ?? string.Empty));
    }

    /// <summary>
    ///     取消控制台输出订阅，委托给 Runtime.ConsoleOutputChannel。
    /// </summary>
    public void UnsubscribeConsoleOutput(long subscriptionId)
    {
        if (subscriptionId <= 0)
            return;
        Runtime.ConsoleOutputChannel?.Unsubscribe(subscriptionId);
    }

    /// <summary>
    ///     流程级字符串栈状态机容器；无 <see cref="IProgressRun" /> 时为 null。
    /// </summary>
    public StateMachineContainer? GetProgressStateMachines() => _progressRun?.ProgressScope.StateMachines;

    /// <summary>
    ///     当前关卡会话级字符串栈状态机容器。
    /// </summary>
    public StateMachineContainer? GetSessionStateMachines() => _progressRun?.CurrentSession?.SessionScope.StateMachines;

    // ── Save flow delegation（委托给 SaveGameWorkflow）─────────────────

    /// <summary>
    ///     列出所有存档槽位 ID，委托给 SaveGameWorkflow。
    /// </summary>
    public IReadOnlyList<string> ListSaves() => _saveGameWorkflow.ListSaves();

    /// <summary>
    ///     列出所有存档槽位及其元数据，委托给 SaveGameWorkflow。
    /// </summary>
    public IReadOnlyList<SaveMetaDataEntry> ListSavesWithMetaData() => _saveGameWorkflow.ListSavesWithMetaData();

    /// <summary>
    ///     请求保存游戏到新槽位，委托给 SaveGameWorkflow。
    /// </summary>
    public void RequestSaveGame(
        string newSaveId,
        string baseSaveId,
        IReadOnlyDictionary<string, string>? customMeta = null)
        => _saveGameWorkflow.RequestSaveGame(newSaveId, baseSaveId, customMeta);

    /// <summary>
    ///     请求加载指定存档，委托给 SaveGameWorkflow。
    /// </summary>
    public void RequestLoadGame(string saveId) => _saveGameWorkflow.RequestLoadGame(saveId);

    /// <summary>
    ///     请求继续游戏（加载 continue 槽位），委托给 SaveGameWorkflow。
    /// </summary>
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

    /// <summary>
    ///     查询是否存在 continue 数据。
    /// </summary>
    public bool HasContinueData() => _saveGameWorkflow.HasContinueData();

    /// <summary>
    ///     设置 continue 目标存档 ID。
    /// </summary>
    public void SetContinueTarget(string saveId) => _saveGameWorkflow.SetContinueTarget(saveId);

    /// <summary>
    ///     清除 continue 目标。
    /// </summary>
    public void ClearContinueTarget() => _saveGameWorkflow.ClearContinueTarget();

    // ── Entry point delegation（委托给 EntryPointWorkflow）─────────────

    /// <summary>
    ///     请求加载初始存档模板，委托给 EntryPointWorkflow。
    /// </summary>
    public void RequestLoadInitialSave() => _entryPointWorkflow.RequestLoadInitialSave();

    /// <summary>
    ///     按启动流程重新读取主菜单 entry 配置（与 OrigoDefaultEntry.ConfigPath 一致）。
    ///     不包含隐式保存；若业务需要保存，请先显式调用 RequestSaveGame/RequestSaveGameAuto。
    /// </summary>
    public void RequestLoadMainMenuEntrySave() => _entryPointWorkflow.RequestLoadMainMenuEntrySave();

    /// <summary>
    ///     请求切换到新关卡，委托给 EntryPointWorkflow。
    /// </summary>
    public void RequestChangeLevel(string newLevelId) => _entryPointWorkflow.RequestChangeLevel(newLevelId);
}
