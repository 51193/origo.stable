using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Meta;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     流程级运行时实现，持有流程黑板与 SessionManager。
///     负责会话生命周期编排与进度持久化。
///     架构上不区分前台/后台会话：前台只是以 <see cref="ISessionManager.ForegroundKey" /> 为键挂载的普通会话。
///     所有会话操作均委托给 <see cref="SessionManager" />，不直接操作 SessionRun 实例。
/// </summary>
public sealed partial class ProgressRun : IProgressRun
{
    private readonly RunFactory _factory;
    private readonly SessionManager _sessionManager;
    private bool _disposed;

    internal ProgressRun(
        RunFactory factory,
        RunStateScope progressScope,
        string saveId)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(progressScope);
        ArgumentNullException.ThrowIfNull(saveId);
        _factory = factory;
        ProgressScope = progressScope;
        SaveId = saveId;
        _sessionManager = new SessionManager(factory, progressScope.Blackboard);
        _sessionManager.SetLogger(factory.Logger);
        factory.Logger.Log(LogLevel.Info, "ProgressRun", $"Created ProgressRun (saveId: '{saveId}').");
    }

    /// <summary>
    ///     流程级状态机容器。
    ///     内部使用，仅通过 ISndContext.GetProgressStateMachines() 暴露给策略层。
    /// </summary>
    internal RunStateScope ProgressScope { get; }

    /// <summary>
    ///     当前前台会话的快捷访问。等价于 <c>SessionManager.ForegroundSession</c>。
    /// </summary>
    internal ISessionRun? ForegroundSession => _sessionManager.ForegroundSession;

    /// <summary>
    ///     流程级黑板。
    /// </summary>
    public IBlackboard ProgressBlackboard => ProgressScope.Blackboard;

    /// <summary>
    ///     会话管理器，统一管理所有挂载的会话。
    /// </summary>
    public ISessionManager SessionManager => _sessionManager;

    public string SaveId { get; private set; }

    /// <inheritdoc />
    public StateMachineContainer GetProgressStateMachines() => ProgressScope.StateMachines;

    public void Dispose()
    {
        if (_disposed) return;
        _factory.Logger.Log(LogLevel.Info, "ProgressRun", $"Disposing ProgressRun (saveId: '{SaveId}').");

        // Auto-persist progress state before cleanup.
        try
        {
            PersistProgress();
        }
        catch (Exception ex)
        {
            // Best-effort: if persistence fails, log warning and continue with cleanup.
            _factory.Logger.Log(LogLevel.Warning, "ProgressRun",
                $"Auto-persist failed during Dispose (saveId: '{SaveId}'): {ex.Message}");
        }

        // Destroy all mounted sessions (each session auto-persists on Dispose via SessionManager).
        _sessionManager.Clear();

        // ProgressRun 生命周期结束时清理 current/ 临时目录，避免残留的临时存档被后续流程误用。
        _factory.StorageService.DeleteCurrentDirectory();

        ProgressScope.StateMachines.PopAllOnQuit();
        ProgressScope.StateMachines.Clear();
        ProgressBlackboard.Clear();

        _disposed = true;
    }

    // Session creation / loading methods moved to ProgressRun.SessionLoading.cs

    internal void SetSaveId(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));
        SaveId = saveId;
    }

    internal SaveMetaBuildContext BuildSaveMetaContext(string saveId)
    {
        var fgSession = ForegroundSession ?? throw new InvalidOperationException("No active foreground session.");
        return new SaveMetaBuildContext(
            saveId,
            fgSession.LevelId,
            ProgressBlackboard,
            fgSession.SessionBlackboard,
            fgSession.SceneHost);
    }

    internal SaveGamePayload BuildSavePayload(
        string newSaveId,
        IReadOnlyDictionary<string, string>? mergedMeta = null)
    {
        var fgSession = ForegroundSession ?? throw new InvalidOperationException("No active foreground session.");
        EnsureActiveLevelInvariant(fgSession);

        // Record background session mount info in progress blackboard BEFORE serialization,
        // so it survives the round-trip and can be used to restore sessions on load.
        // Format: "mountKey=levelId=syncProcess,mountKey=levelId=syncProcess,..."
        var bgSessions = _sessionManager.GetBackgroundSessions();
        if (bgSessions.Count > 0)
        {
            var bgIds = string.Join(",", bgSessions.Select(kvp =>
            {
                var syncProcess = _sessionManager.GetSyncProcess(kvp.Key);
                return $"{kvp.Key}={kvp.Value.LevelId}={syncProcess.ToString().ToLowerInvariant()}";
            }));
            ProgressBlackboard.Set(WellKnownKeys.BackgroundLevelIds, bgIds);
        }
        else
        {
            ProgressBlackboard.Set(WellKnownKeys.BackgroundLevelIds, string.Empty);
        }

        var saveContext = _factory.CreateSaveContext(ProgressBlackboard, fgSession.SessionBlackboard);

        var jsonCodec = _factory.Runtime.SndWorld.JsonCodec;
        var converterRegistry = _factory.Runtime.SndWorld.ConverterRegistry;
        var progressSmJson = ProgressScope.StateMachines.SerializeToDataSource(jsonCodec, converterRegistry);
        var sessionSmJson = fgSession.GetSessionStateMachines().SerializeToDataSource(jsonCodec, converterRegistry);

        var payload = saveContext.SaveGame(
            fgSession.SceneHost,
            newSaveId,
            fgSession.LevelId,
            mergedMeta,
            progressSmJson,
            sessionSmJson);

        // Serialize each background session via SessionManager and include in the payload.
        var bgPayloads = _sessionManager.SerializeBackgroundSessions();
        foreach (var kvp in bgSessions)
            if (bgPayloads.TryGetValue(kvp.Key, out var bgPayload))
                payload.Levels[kvp.Value.LevelId] = bgPayload;

        return payload;
    }

    /// <summary>
    ///     保存前校验：流程黑板必须包含与前台会话一致的 <see cref="WellKnownKeys.ActiveLevelId" />。
    /// </summary>
    private void EnsureActiveLevelInvariant(ISessionRun fgSession)
    {
        var (found, id) = ProgressBlackboard.TryGet<string>(WellKnownKeys.ActiveLevelId);
        if (!found || string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException(
                $"Progress blackboard missing required '{WellKnownKeys.ActiveLevelId}' before save (save id: '{SaveId}').");

        if (!string.Equals(id, fgSession.LevelId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Progress '{WellKnownKeys.ActiveLevelId}' ('{id}') does not match foreground level '{fgSession.LevelId}' (save id: '{SaveId}').");
    }

    // PersistProgress moved to ProgressRun.Persistence.cs

    // SwitchForeground moved to ProgressRun.SessionLoading.cs
}
