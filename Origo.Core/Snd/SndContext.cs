using System;
using System.Collections.Generic;
using System.Threading;
using System.Text.Json;
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
/// </summary>
public sealed partial class SndContext
{
    private const string DefaultInitialSaveId = "000";
    private const string DefaultInitialLevelId = "default";
    private const string DefaultMainMenuLevelId = "main_menu";
    private readonly IBlackboard _emptyBlackboard = new Blackboard.Blackboard();
    private readonly RunFactory _runFactory;
    private readonly List<ISaveMetaContributor> _saveMetaContributors = new();
    private int _pendingPersistenceRequests;
    private readonly ISystemRun _systemRun;
    private IProgressRun? _progressRun;

    public SndContext(
        OrigoRuntime runtime,
        IFileSystem fileSystem,
        string saveRootPath,
        string initialSaveRootPath,
        string entryConfigPath)
    {
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

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

        _runFactory = new RunFactory(Runtime.Logger, FileSystem, SaveRootPath, Runtime, this);
        _systemRun = _runFactory.CreateSystemRun();
    }

    internal OrigoRuntime Runtime { get; }

    internal IFileSystem FileSystem { get; }

    public string SaveRootPath { get; }

    public string InitialSaveRootPath { get; }
    public string EntryConfigPath { get; }

    public IBlackboard SystemBlackboard => _systemRun.SystemBlackboard;
    public IBlackboard ProgressBlackboard => _progressRun?.ProgressBlackboard ?? _emptyBlackboard;
    public IBlackboard SessionBlackboard => _progressRun?.CurrentSession?.SessionBlackboard ?? _emptyBlackboard;
    public SndRuntime SndRuntime => Runtime.Snd;

    private JsonSerializerOptions JsonOptions => Runtime.SndWorld.JsonOptions;

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
        return Volatile.Read(ref _pendingPersistenceRequests);
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
        var clonedJson = Runtime.SndWorld.SerializeMeta(template);
        var cloned = Runtime.SndWorld.DeserializeMeta(clonedJson);
        if (!string.IsNullOrWhiteSpace(overrideName))
            cloned.Name = overrideName;
        return cloned;
    }

    public bool TrySubmitConsoleCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return false;

        Runtime.ConsoleInput?.Enqueue(commandLine.Trim());
        return Runtime.ConsoleInput != null;
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

    private void ShutdownCurrentProgressAndScene()
    {
        _progressRun?.Dispose();
        _progressRun = null;
        Runtime.Snd.ClearAll();
    }

    // Methods split into:
    // - SndContext.SaveFlow.cs
    // - SndContext.Entry.cs
    // - SndContext.ActiveSaveState.cs
    // - SndContext.SaveMeta.cs
}