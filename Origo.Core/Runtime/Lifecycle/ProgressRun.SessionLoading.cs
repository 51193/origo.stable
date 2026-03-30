using System;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    public ISessionRun? CreateFromActiveLevel()
    {
        LoadSessionRunFromCurrent();
        return _currentSession;
    }

    public ISessionRun? CreateFromAlreadyLoadedScene()
    {
        CreateEmptySessionRun(false);
        return _currentSession;
    }

    public void LoadFromPayload(SaveGamePayload payload)
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
            _factory.Runtime.SndWorld.JsonOptions);

        var activeLevel = payload.ActiveLevelId;
        if (string.IsNullOrWhiteSpace(activeLevel))
            throw new InvalidOperationException("Payload.ActiveLevelId cannot be null or whitespace.");

        UpdateActiveLevel(activeLevel);

        if (payload.Levels.TryGetValue(ActiveLevelId, out var levelPayload))
            CreateSessionRunFromPayload(levelPayload);
        else
            CreateEmptySessionRun(true);
    }

    private void LoadSessionRunFromCurrent()
    {
        // 严格语义：current 目录应始终包含完整关卡 payload；缺文件即抛。
        var payload = SaveStorageFacade.ReadSavePayloadFromCurrent(
            _factory.FileSystem,
            _factory.SaveRootPath,
            SaveId,
            ActiveLevelId);

        if (!payload.Levels.TryGetValue(ActiveLevelId, out var levelPayload))
            throw new InvalidOperationException(
                $"Current save payload missing required level '{ActiveLevelId}'.");

        CreateSessionRunFromPayload(levelPayload);
    }

    private void CreateSessionRunFromPayload(LevelPayload levelPayload)
    {
        _currentSession?.Dispose();

        var sessionBlackboard = new Blackboard.Blackboard();
        var saveContext = _factory.CreateSaveContext(
            ProgressBlackboard,
            sessionBlackboard);
        saveContext.DeserializeSession(levelPayload.SessionJson);

        var newSession = _factory.CreateSessionRun(
            saveContext,
            ActiveLevelId,
            sessionBlackboard,
            _factory.Runtime.Snd.SceneHost);

        if (string.IsNullOrWhiteSpace(levelPayload.SessionStateMachinesJson))
            throw new InvalidOperationException("Level payload missing required SessionStateMachinesJson.");

        newSession.SessionScope.StateMachines.DeserializeWithoutHooks(
            levelPayload.SessionStateMachinesJson,
            _factory.Runtime.SndWorld.JsonOptions);

        // 先创建 SessionRun / SessionStateMachines 并恢复存档栈，
        // 再 Load 场景（AfterLoad 期间策略可读取到已恢复的 session 级状态机；且不会被后续 Import 覆盖）。
        saveContext.DeserializeSndScene(
            _factory.Runtime.Snd.SceneHost,
            levelPayload.SndSceneJson);

        // Only assign after all initialization succeeds to prevent partial state.
        _currentSession = newSession;

        FlushStateMachinesAfterSceneReady();
    }

    private void CreateEmptySessionRun(bool clearScene)
    {
        _currentSession?.Dispose();

        if (clearScene)
            _factory.Runtime.Snd.ClearAll();

        var sessionBlackboard = new Blackboard.Blackboard();
        var saveContext = _factory.CreateSaveContext(
            ProgressBlackboard,
            sessionBlackboard);

        _currentSession = _factory.CreateSessionRun(
            saveContext,
            ActiveLevelId,
            sessionBlackboard,
            _factory.Runtime.Snd.SceneHost);

        FlushStateMachinesAfterSceneReady();
    }

    private void FlushStateMachinesAfterSceneReady()
    {
        ProgressScope.StateMachines.FlushAllAfterLoad();
        _currentSession?.SessionScope.StateMachines.FlushAllAfterLoad();
    }
}