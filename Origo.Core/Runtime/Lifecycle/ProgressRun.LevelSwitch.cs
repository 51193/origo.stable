using System;
using System.Text.Json;
using Origo.Core.Save;
using Origo.Core.StateMachine;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    public void SwitchLevel(string newLevelId)
    {
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException("New level id cannot be null or whitespace.", nameof(newLevelId));
        // 1) SessionRun.PersistLevelState()
        _currentSession?.PersistLevelState();

        // (preflight) strict parsing: if target payload exists, it must be valid and loadable.
        var targetLevelPayload = SavePayloadReader.TryReadLevelPayloadFromCurrent(
            _factory.FileSystem,
            _factory.SaveRootPath,
            newLevelId);

        var shouldLoadFromPayload = false;
        if (targetLevelPayload != null)
        {
            if (string.IsNullOrWhiteSpace(targetLevelPayload.SndSceneJson))
                throw new InvalidOperationException(
                    $"Target level '{newLevelId}' has invalid snd_scene.json (empty).");
            if (string.IsNullOrWhiteSpace(targetLevelPayload.SessionJson))
                throw new InvalidOperationException(
                    $"Target level '{newLevelId}' has invalid session.json (empty).");
            if (string.IsNullOrWhiteSpace(targetLevelPayload.SessionStateMachinesJson))
                throw new InvalidOperationException(
                    $"Target level '{newLevelId}' has invalid session_state_machines.json (empty).");

            // Validate state machine JSON parses before committing switch.
            _ = JsonSerializer.Deserialize<StateMachineContainerPayload>(
                    targetLevelPayload.SessionStateMachinesJson,
                    _factory.Runtime.SndWorld.JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Target level '{newLevelId}' has invalid session state machines json (null payload).");
            shouldLoadFromPayload = true;
        }

        // 2) ProgressRun.UpdateActiveLevel(newLevelId) + PersistProgress()
        UpdateActiveLevel(newLevelId);
        PersistProgress();

        // 3) SessionRun.Dispose()
        _currentSession?.Dispose();
        _currentSession = null;

        // 4) 尝试从 current/level_{id} 恢复；若无完整数据则创建空会话并清场（README 约定）。
        if (shouldLoadFromPayload)
            CreateSessionRunFromPayload(targetLevelPayload!);
        else
            CreateEmptySessionRun(true);
    }
}