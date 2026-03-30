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

        // Phase 1: Persist current session (non-destructive).
        PersistCurrentSession();

        // Phase 2: Pre-flight validation – all checks run before any state mutation.
        var (shouldLoadFromPayload, targetLevelPayload) = ValidateTargetLevel(newLevelId);

        // Phase 3: Commit – unload old session and load new one.
        //   UpdateActiveLevel + PersistProgress may fail; if they do, the old session
        //   is still intact. The destructive Dispose only runs after persistence succeeds.
        var previousLevelId = ActiveLevelId;
        try
        {
            UpdateActiveLevel(newLevelId);
            PersistProgress();
        }
        catch
        {
            // Rollback: restore the previous level id so ProgressBlackboard stays consistent.
            ActiveLevelId = previousLevelId;
            ProgressBlackboard.Set(WellKnownKeys.ActiveLevelId, previousLevelId);
            throw;
        }

        _currentSession?.Dispose();
        _currentSession = null;

        LoadOrCreateNewSession(newLevelId, shouldLoadFromPayload, targetLevelPayload);
    }

    private void PersistCurrentSession()
    {
        _currentSession?.PersistLevelState();
    }

    private (bool shouldLoad, LevelPayload? payload) ValidateTargetLevel(string newLevelId)
    {
        var targetLevelPayload = SavePayloadReader.TryReadLevelPayloadFromCurrent(
            _factory.FileSystem,
            _factory.SaveRootPath,
            newLevelId);

        if (targetLevelPayload is null)
            return (false, null);

        if (string.IsNullOrWhiteSpace(targetLevelPayload.SndSceneJson))
            throw new InvalidOperationException(
                $"Target level '{newLevelId}' has invalid snd_scene.json (empty).");
        if (string.IsNullOrWhiteSpace(targetLevelPayload.SessionJson))
            throw new InvalidOperationException(
                $"Target level '{newLevelId}' has invalid session.json (empty).");
        if (string.IsNullOrWhiteSpace(targetLevelPayload.SessionStateMachinesJson))
            throw new InvalidOperationException(
                $"Target level '{newLevelId}' has invalid session_state_machines.json (empty).");

        _ = JsonSerializer.Deserialize<StateMachineContainerPayload>(
                targetLevelPayload.SessionStateMachinesJson,
                _factory.Runtime.SndWorld.JsonOptions)
            ?? throw new InvalidOperationException(
                $"Target level '{newLevelId}' has invalid session state machines json (null payload).");

        return (true, targetLevelPayload);
    }

    private void LoadOrCreateNewSession(string newLevelId, bool shouldLoadFromPayload, LevelPayload? payload)
    {
        if (shouldLoadFromPayload)
            CreateSessionRunFromPayload(payload!);
        else
            CreateEmptySessionRun(true);
    }
}