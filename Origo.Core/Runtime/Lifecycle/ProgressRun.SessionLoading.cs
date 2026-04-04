using System;
using Origo.Core.Save;
using Origo.Core.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    /// <summary>
    ///     从完整 save payload 恢复流程状态（progress blackboard、状态机）并挂载前台会话。
    /// </summary>
    internal void LoadFromPayload(SaveGamePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var saveContext = _factory.CreateSaveContext(
            ProgressBlackboard,
            new Blackboard.Blackboard());
        saveContext.DeserializeProgress(payload.ProgressJson);

        if (string.IsNullOrWhiteSpace(payload.ProgressStateMachinesJson))
            throw new InvalidOperationException("Save payload missing required ProgressStateMachinesJson.");

        ProgressScope.StateMachines.DeserializeWithoutHooks(
            payload.ProgressStateMachinesJson,
            _factory.Runtime.SndWorld.JsonCodec,
            _factory.Runtime.SndWorld.ConverterRegistry);

        var activeLevel = payload.ActiveLevelId;
        if (string.IsNullOrWhiteSpace(activeLevel))
            throw new InvalidOperationException("Payload.ActiveLevelId cannot be null or whitespace.");

        if (payload.Levels.TryGetValue(activeLevel, out var levelPayload))
            MountForegroundFromPayload(activeLevel, levelPayload);
        else
            MountEmptyForeground(activeLevel);

        // Restore background sessions from the payload if recorded.
        RestoreBackgroundSessions(payload);

        VerifyProgressActiveLevelInvariant(activeLevel);
    }

    /// <summary>
    ///     从存档模块加载指定关卡并挂载为前台会话。
    ///     存档模块自行处理 current/ vs snapshot 查找逻辑，外部不需感知存储位置。
    /// </summary>
    internal ISessionRun LoadAndMountForeground(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));

        var levelPayload = _factory.StorageService.ResolveLevelPayload(SaveId, levelId);
        if (levelPayload is not null)
        {
            ValidateLevelPayload(levelId, levelPayload);
            return MountForegroundFromPayload(levelId, levelPayload);
        }

        // No data found — mount an empty session.
        return MountEmptyForeground(levelId);
    }

    /// <summary>
    ///     切换前台会话：先持久化并卸载旧前台，再加载并挂载新前台。
    ///     这是一个同步操作，应通过延迟队列在帧末调用。
    /// </summary>
    internal void SwitchForeground(string newLevelId)
    {
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException("New level id cannot be null or whitespace.", nameof(newLevelId));

        // Phase 1: Persist progress state.
        PersistProgress();

        // Phase 2: Unload old foreground (Dispose auto-persists session).
        _sessionManager.DestroyForeground();
        _factory.Runtime.Snd.ClearAll();

        // Phase 3: Load new foreground via unified path.
        LoadAndMountForeground(newLevelId);
    }

    private ISessionRun MountForegroundFromPayload(string levelId, LevelPayload levelPayload)
    {
        _sessionManager.DestroyForeground();

        var session = _sessionManager.CreateForegroundFromPayload(
            levelId, _factory.Runtime.Snd.SceneHost, levelPayload);
        ProgressScope.StateMachines.FlushAllAfterLoad();
        SyncActiveLevelIdToProgress(levelId);
        return session;
    }

    private ISessionRun MountEmptyForeground(string levelId)
    {
        _sessionManager.DestroyForeground();
        _factory.Runtime.Snd.ClearAll();

        var session = _sessionManager.CreateForegroundSession(levelId, _factory.Runtime.Snd.SceneHost);
        FlushStateMachinesAfterSceneReady();
        SyncActiveLevelIdToProgress(levelId);
        return session;
    }

    /// <summary>
    ///     前台会话挂载成功后，将 <see cref="WellKnownKeys.ActiveLevelId" /> 写入流程黑板，
    ///     保证序列化出的 progress.json 满足读档契约。
    /// </summary>
    private void SyncActiveLevelIdToProgress(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));
        ProgressBlackboard.Set(WellKnownKeys.ActiveLevelId, levelId);
    }

    /// <summary>
    ///     校验流程黑板中的活跃关卡与前台会话、以及加载时 payload 声明的关卡一致。
    /// </summary>
    private void VerifyProgressActiveLevelInvariant(string expectedActiveLevelId)
    {
        var fg = ForegroundSession
                 ?? throw new InvalidOperationException("No active foreground session after load.");

        if (!string.Equals(fg.LevelId, expectedActiveLevelId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Foreground session level '{fg.LevelId}' does not match expected active level '{expectedActiveLevelId}'.");

        var (found, id) = ProgressBlackboard.TryGet<string>(WellKnownKeys.ActiveLevelId);
        if (!found || string.IsNullOrWhiteSpace(id)
                   || !string.Equals(id, expectedActiveLevelId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Progress blackboard missing or mismatched '{WellKnownKeys.ActiveLevelId}': expected '{expectedActiveLevelId}'.");
    }

    private void FlushStateMachinesAfterSceneReady()
    {
        ProgressScope.StateMachines.FlushAllAfterLoad();
        ForegroundSession?.GetSessionStateMachines().FlushAllAfterLoad();
    }

    private void ValidateLevelPayload(string levelId, LevelPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.SndSceneJson))
            throw new InvalidOperationException($"Target level '{levelId}' has invalid snd_scene.json (empty).");
        if (string.IsNullOrWhiteSpace(payload.SessionJson))
            throw new InvalidOperationException($"Target level '{levelId}' has invalid session.json (empty).");
        if (string.IsNullOrWhiteSpace(payload.SessionStateMachinesJson))
            throw new InvalidOperationException(
                $"Target level '{levelId}' has invalid session_state_machines.json (empty).");

        var smCodec = _factory.Runtime.SndWorld.JsonCodec;
        var smRegistry = _factory.Runtime.SndWorld.ConverterRegistry;
        using var smNode = smCodec.Decode(payload.SessionStateMachinesJson);
        _ = smRegistry.Read<StateMachineContainerPayload>(smNode)
            ?? throw new InvalidOperationException(
                $"Target level '{levelId}' has invalid session state machines json (null payload).");
    }

    /// <summary>
    ///     从进度黑板中读取 <see cref="WellKnownKeys.BackgroundLevelIds" />，
    ///     恢复所有持久化的后台会话并重新挂载到 SessionManager。
    /// </summary>
    private void RestoreBackgroundSessions(SaveGamePayload payload)
    {
        var (found, bgIdsRaw) = ProgressBlackboard.TryGet<string>(WellKnownKeys.BackgroundLevelIds);
        if (!found || string.IsNullOrWhiteSpace(bgIdsRaw))
            return;

        // Format: "mountKey=levelId=syncProcess,mountKey=levelId=syncProcess,..."
        // Backward-compatible: old format "mountKey=levelId" (2 parts) defaults syncProcess to false.
        var entries = bgIdsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries)
        {
            var parts = entry.Split('=');
            if (parts.Length < 2) continue;

            var mountKey = parts[0];
            var levelId = parts[1];
            var syncProcess = parts.Length >= 3
                              && bool.TryParse(parts[2], out var parsedSyncProcess)
                              && parsedSyncProcess;

            if (string.IsNullOrWhiteSpace(mountKey) || string.IsNullOrWhiteSpace(levelId))
                continue;

            if (payload.Levels.TryGetValue(levelId, out var bgLevelPayload))
                _sessionManager.CreateBackgroundSessionFromPayload(mountKey, levelId, bgLevelPayload,
                    syncProcess);
        }
    }
}
