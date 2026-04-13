using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Serialization;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     流程级运行时实现。
///     构造时接收 <see cref="SystemRuntime" /> 与 <see cref="ProgressParameters" />，
///     内部基于 SystemRuntime 构建 <see cref="ProgressRuntime" /> 作为本层唯一运行时容器。
///     <para>
///         SessionManager 作为独立的运行时构造层，由 ProgressRun 创建并持有。
///         所有会话操作均委托给 <see cref="SessionManager" />。
///     </para>
/// </summary>
public sealed partial class ProgressRun : IDisposable
{
    private readonly ProgressRuntime _progressRuntime;
    private readonly SessionManager _sessionManager;
    private bool _disposed;

    internal ProgressRun(
        SystemRuntime systemRuntime,
        ProgressParameters progressParams,
        IStateMachineContext stateMachineContext,
        ISndContext sndContext)
    {
        ArgumentNullException.ThrowIfNull(systemRuntime);
        ArgumentNullException.ThrowIfNull(stateMachineContext);
        ArgumentNullException.ThrowIfNull(sndContext);
        if (string.IsNullOrWhiteSpace(progressParams.SaveId))
            throw new ArgumentException("Save id cannot be null or whitespace.");

        _progressRuntime = new ProgressRuntime(systemRuntime, stateMachineContext, sndContext);

        var progressBlackboard = new Blackboard.Blackboard();
        var progressMachines = new StateMachineContainer(
            _progressRuntime.SndWorld.StrategyPool, stateMachineContext);
        ProgressScope = new RunStateScope(progressBlackboard, progressMachines);
        SaveId = progressParams.SaveId;

        _sessionManager = new SessionManager(
            _progressRuntime,
            new SessionManagerParameters(),
            ProgressScope.Blackboard);

        _progressRuntime.Logger.Log(LogLevel.Info, "ProgressRun",
            $"Created ProgressRun (saveId: '{progressParams.SaveId}').");
    }

    internal RunStateScope ProgressScope { get; }

    internal ISessionRun? ForegroundSession => _sessionManager.ForegroundSession;

    public IBlackboard ProgressBlackboard => ProgressScope.Blackboard;

    public ISessionManager SessionManager => _sessionManager;

    public string SaveId { get; private set; }

    public void Dispose()
    {
        if (_disposed) return;
        // Set flag first to prevent recursive Dispose calls (e.g. from cleanup callbacks).
        _disposed = true;
        _progressRuntime.Logger.Log(LogLevel.Info, "ProgressRun",
            $"Disposing ProgressRun (saveId: '{SaveId}').");

        try
        {
            PersistProgress();
        }
        catch (Exception ex)
        {
            _progressRuntime.Logger.Log(LogLevel.Warning, "ProgressRun",
                $"Auto-persist failed during Dispose (saveId: '{SaveId}'): {ex.Message}");
        }

        _sessionManager.Clear();
        _progressRuntime.StorageService.DeleteCurrentDirectory();

        ProgressScope.StateMachines.PopAllOnQuit();
        ProgressScope.StateMachines.Clear();
        ProgressBlackboard.Clear();
    }

    /// <inheritdoc />
    public StateMachineContainer GetProgressStateMachines() => ProgressScope.StateMachines;

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

        var bgSessions = _sessionManager.GetBackgroundSessions();
        var topologyItems = new List<string>
        {
            SessionTopologyCodec.Serialize(ISessionManager.ForegroundKey, fgSession.LevelId, false)
        };
        topologyItems.AddRange(bgSessions.Select(kvp =>
        {
            var syncProcess = _sessionManager.GetSyncProcess(kvp.Key);
            return SessionTopologyCodec.Serialize(kvp.Key, kvp.Value.LevelId, syncProcess);
        }));
        ProgressBlackboard.Set(WellKnownKeys.SessionTopology, SessionTopologyCodec.Join(topologyItems));

        var saveContext = new SaveContext(
            ProgressBlackboard, fgSession.SessionBlackboard, _progressRuntime.SndWorld);

        var progressSmJson = ProgressScope.StateMachines.SerializeToDataSource(
            _progressRuntime.JsonCodec, _progressRuntime.ConverterRegistry);
        var sessionSmJson = fgSession.GetSessionStateMachines().SerializeToDataSource(
            _progressRuntime.JsonCodec, _progressRuntime.ConverterRegistry);

        var payload = saveContext.SaveGame(
            fgSession.SceneHost,
            newSaveId,
            fgSession.LevelId,
            mergedMeta,
            progressSmJson,
            sessionSmJson);

        var bgPayloads = _sessionManager.SerializeBackgroundSessions();
        foreach (var kvp in bgSessions)
            if (bgPayloads.TryGetValue(kvp.Key, out var bgPayload))
                payload.Levels[kvp.Value.LevelId] = bgPayload;

        return payload;
    }

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
}
