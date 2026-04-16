using System;
using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Origo.Core.Save.Serialization;
using Origo.Core.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    /// <summary>
    ///     从 <see cref="SaveGamePayload" /> 中恢复 ProgressRun 的全部状态。
    ///     遵循职责链模式：
    ///     <list type="number">
    ///         <item>ProgressRun 反序列化自身私有数据（ProgressBlackboard、进度状态机）。</item>
    ///         <item>从 ProgressBlackboard 中读取 <see cref="WellKnownKeys.SessionTopology" /> 获取会话元信息列表。</item>
    ///         <item>将元信息列表传递给 SessionManager，由其逐个创建并挂载 SessionRun。</item>
    ///         <item>SessionRun 在创建后由 SessionManager 调用 LoadFromPayload，自行反序列化自身数据。</item>
    ///     </list>
    ///     每层模块仅处理自身拥有的数据，并将控制权委托给下一层。
    /// </summary>
    internal void LoadFromPayload(SaveGamePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // ── Step 1: ProgressRun deserializes its own private data ──
        var saveContext = new SaveContext(
            ProgressBlackboard, new Blackboard.Blackboard(), _progressRuntime.SndWorld);
        saveContext.DeserializeProgress(payload.ProgressNode);

        if (payload.ProgressStateMachinesNode.IsNull)
            throw new InvalidOperationException("Save payload missing required ProgressStateMachinesNode.");

        ProgressScope.StateMachines.DeserializeFromNode(
            payload.ProgressStateMachinesNode,
            _progressRuntime.ConverterRegistry);

        // ── Step 2: Read session topology from self-owned ProgressBlackBoard ──
        // ProgressRun reads the topology from its own blackboard (just deserialized above),
        // then delegates session creation to SessionManager.
        var topology = ParseSessionTopologyFromProgress();
        if (topology.Count == 0)
            throw new InvalidOperationException(
                $"Progress blackboard missing required '{WellKnownKeys.SessionTopology}' before load.");

        // ── Step 3: Delegate to SessionManager → SessionRun (Chain of Responsibility) ──
        _sessionManager.Clear();
        foreach (var descriptor in topology)
            MountSessionFromDescriptor(payload, descriptor);

        var fg = ForegroundSession
                 ?? throw new InvalidOperationException("No active foreground session after topology restore.");
        VerifyProgressActiveLevelInvariant(fg.LevelId);
    }

    internal ISessionRun LoadAndMountForeground(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));

        var levelPayload = _progressRuntime.StorageService.ResolveLevelPayload(SaveId, levelId);
        if (levelPayload is not null)
        {
            ValidateLevelPayload(levelId, levelPayload);
            return MountForegroundFromPayload(levelId, levelPayload);
        }

        return MountEmptyForeground(levelId);
    }

    internal void SwitchForeground(string newLevelId)
    {
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException("New level id cannot be null or whitespace.", nameof(newLevelId));

        PersistProgress();
        _sessionManager.DestroyForeground();
        _progressRuntime.SndRuntime.ClearAll();
        LoadAndMountForeground(newLevelId);
    }

    private ISessionRun MountForegroundFromPayload(string levelId, LevelPayload levelPayload)
    {
        _sessionManager.DestroyForeground();

        var session = _sessionManager.CreateForegroundFromPayload(
            levelId, _progressRuntime.ForegroundSceneHost, levelPayload);
        ProgressScope.StateMachines.FlushAllAfterLoad();
        SyncForegroundTopologyToProgress(levelId);
        return session;
    }

    private ISessionRun MountEmptyForeground(string levelId)
    {
        _sessionManager.DestroyForeground();
        _progressRuntime.SndRuntime.ClearAll();

        var session = _sessionManager.CreateForegroundSession(levelId, _progressRuntime.ForegroundSceneHost);
        FlushStateMachinesAfterSceneReady();
        SyncForegroundTopologyToProgress(levelId);
        return session;
    }

    private void SyncForegroundTopologyToProgress(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));
        ProgressBlackboard.Set(WellKnownKeys.SessionTopology,
            SessionTopologyCodec.Serialize(ISessionManager.ForegroundKey, levelId, false));
    }

    private void VerifyProgressActiveLevelInvariant(string expectedActiveLevelId)
    {
        var fg = ForegroundSession
                 ?? throw new InvalidOperationException("No active foreground session after load.");

        if (!string.Equals(fg.LevelId, expectedActiveLevelId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Foreground session level '{fg.LevelId}' does not match expected active level '{expectedActiveLevelId}'.");

        var (found, rawTopology) = ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
        if (!found || string.IsNullOrWhiteSpace(rawTopology))
            throw new InvalidOperationException(
                $"Progress blackboard missing required '{WellKnownKeys.SessionTopology}': expected foreground '{expectedActiveLevelId}'.");

        var topologyActiveLevelId = SessionTopologyCodec.ExtractForegroundLevelId(rawTopology);
        if (!string.Equals(topologyActiveLevelId, expectedActiveLevelId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Progress '{WellKnownKeys.SessionTopology}' foreground ('{topologyActiveLevelId}') does not match expected active level '{expectedActiveLevelId}'.");
    }

    private void FlushStateMachinesAfterSceneReady()
    {
        ProgressScope.StateMachines.FlushAllAfterLoad();
        ForegroundSession?.GetSessionStateMachines().FlushAllAfterLoad();
    }

    private void ValidateLevelPayload(string levelId, LevelPayload payload)
    {
        if (IsNullOrInvalid(payload.SndSceneNode))
            throw new InvalidOperationException($"Target level '{levelId}' has invalid snd_scene.json (empty).");
        if (IsNullOrInvalid(payload.SessionNode))
            throw new InvalidOperationException($"Target level '{levelId}' has invalid session.json (empty).");
        if (IsNullOrInvalid(payload.SessionStateMachinesNode))
            throw new InvalidOperationException(
                $"Target level '{levelId}' has invalid session_state_machines.json (empty).");

        var smRegistry = _progressRuntime.ConverterRegistry;
        _ = smRegistry.Read<StateMachineContainerPayload>(payload.SessionStateMachinesNode)
            ?? throw new InvalidOperationException(
                $"Target level '{levelId}' has invalid session state machines json (null payload).");
    }

    private static bool IsNullOrInvalid(DataSourceNode node)
    {
        try
        {
            return node.IsNull;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    ///     从 <see cref="SaveGamePayload" /> 中恢复单个会话（含数据）。
    ///     Chain of Responsibility：ProgressRun 解析拓扑 → SessionManager 创建会话 → SessionRun 反序列化自身数据。
    /// </summary>
    private void MountSessionFromDescriptor(SaveGamePayload payload, SessionTopologyCodec.SessionDescriptor descriptor)
    {
        if (string.Equals(descriptor.Key, ISessionManager.ForegroundKey, StringComparison.Ordinal))
        {
            if (payload.Levels.TryGetValue(descriptor.LevelId, out var fgPayload))
                MountForegroundFromPayload(descriptor.LevelId, fgPayload);
            else
                MountEmptyForeground(descriptor.LevelId);
            return;
        }

        _sessionManager.CreateBackgroundSession(
            descriptor.Key, descriptor.LevelId, descriptor.SyncProcess);
        if (payload.Levels.TryGetValue(descriptor.LevelId, out var bgPayload))
            _sessionManager.LoadSessionFromPayload(descriptor.Key, bgPayload);
    }

    private List<SessionTopologyCodec.SessionDescriptor> ParseSessionTopologyFromProgress()
    {
        var (found, raw) = ProgressBlackboard.TryGet<string>(WellKnownKeys.SessionTopology);
        if (!found || string.IsNullOrWhiteSpace(raw))
            return new List<SessionTopologyCodec.SessionDescriptor>();

        return SessionTopologyCodec.Parse(raw);
    }
}
