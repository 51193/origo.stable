using System;
using System.Collections.Generic;
using System.Threading;
using System.Text.Json;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;

namespace Origo.Core.Snd;

public sealed partial class SndContext
{
    public IReadOnlyList<string> ListSaves()
    {
        return SaveStorageFacade.EnumerateSaveIds(FileSystem, SaveRootPath);
    }

    public IReadOnlyList<SaveMetaDataEntry> ListSavesWithMetaData()
    {
        return SaveStorageFacade.EnumerateSavesWithMetaData(FileSystem, SaveRootPath);
    }

    public void RequestSaveGame(
        string newSaveId,
        string baseSaveId,
        IReadOnlyDictionary<string, string>? customMeta = null)
    {
        if (string.IsNullOrWhiteSpace(newSaveId))
            throw new ArgumentException("New save id cannot be null or whitespace.", nameof(newSaveId));
        if (string.IsNullOrWhiteSpace(baseSaveId))
            throw new ArgumentException("Base save id cannot be null or whitespace.", nameof(baseSaveId));

        Interlocked.Increment(ref _pendingPersistenceRequests);
        EnqueueSystemDeferred(() =>
        {
            try
            {
                ExecuteSaveGameNow(newSaveId, baseSaveId, customMeta);
            }
            finally
            {
                Interlocked.Decrement(ref _pendingPersistenceRequests);
            }
        });
    }

    public void RequestLoadGame(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        Interlocked.Increment(ref _pendingPersistenceRequests);
        EnqueueSystemDeferred(() =>
        {
            try
            {
                Runtime.ResetConsoleState();
                _progressRun = LoadOrContinueStrict(saveId);
            }
            finally
            {
                Interlocked.Decrement(ref _pendingPersistenceRequests);
            }
        });
    }

    public bool RequestContinueGame()
    {
        var saveId = TryGetActiveSaveId();
        if (string.IsNullOrWhiteSpace(saveId))
            return false;

        RequestLoadGame(saveId);
        return true;
    }

    /// <summary>
    ///     自动保存请求：
    ///     - baseSaveId 优先使用 SystemBlackboard 中的 active save id
    ///     - newSaveId 未指定时使用 Unix 毫秒时间戳
    /// </summary>
    public string RequestSaveGameAuto(
        string? newSaveId = null,
        IReadOnlyDictionary<string, string>? customMeta = null)
    {
        var baseSaveId = TryGetActiveSaveId() ?? DefaultInitialSaveId;
        var effectiveNewSaveId = string.IsNullOrWhiteSpace(newSaveId)
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            : newSaveId;
        RequestSaveGame(effectiveNewSaveId, baseSaveId, customMeta);
        return effectiveNewSaveId;
    }

    private void ExecuteSaveGameNow(
        string newSaveId,
        string baseSaveId,
        IReadOnlyDictionary<string, string>? customMeta)
    {
        var (progressRun, sessionRun) = EnsureProgressAndSession();
        var currentLevelId = progressRun.ActiveLevelId;

        // 存档前由编排层显式同步关卡索引到 Progress 黑板（不再由 SaveGamePayloadFactory 隐式写入）。
        progressRun.UpdateActiveLevel(currentLevelId);

        var saveContext = new SaveContext(
            progressRun.ProgressBlackboard,
            sessionRun.SessionBlackboard,
            Runtime.SndWorld);

        var metaContext = new SaveMetaBuildContext(
            newSaveId,
            currentLevelId,
            progressRun.ProgressBlackboard,
            sessionRun.SessionBlackboard,
            Runtime.Snd.SceneHost);

        var mergedMeta = SaveMetaMerger.Merge(_saveMetaContributors, in metaContext, customMeta);

        var progressSmJson = progressRun.ProgressScope.StateMachines.ExportToJson(JsonOptions);
        var sessionSmJson = sessionRun.SessionScope.StateMachines.ExportToJson(JsonOptions);

        var payload = saveContext.SaveGame(
            Runtime.Snd.SceneHost,
            newSaveId,
            currentLevelId,
            mergedMeta,
            progressSmJson,
            sessionSmJson);

        SaveStorageFacade.WriteSavePayloadToCurrent(FileSystem, SaveRootPath, payload);
        SaveStorageFacade.SnapshotCurrentToSave(
            FileSystem,
            SaveRootPath,
            baseSaveId,
            newSaveId);

        progressRun.SetSaveId(newSaveId);
        SetActiveSaveState(newSaveId);
    }

    public bool HasContinueData()
    {
        var saveId = TryGetActiveSaveId();
        return !string.IsNullOrWhiteSpace(saveId);
    }

    public void SetContinueTarget(string saveId)
    {
        SetActiveSaveState(saveId);
    }

    public void ClearContinueTarget()
    {
        SystemBlackboard.Set(WellKnownKeys.ActiveSaveId, string.Empty);
    }

    private IProgressRun LoadOrContinueStrict(string saveId)
    {
        // 严格语义：在实体 AfterLoad 触发前必须先让 SndContext 能拿到 Progress/Session 容器。
        // 因此读档编排必须在 SndContext 内完成（不能等 SystemRun 返回后再赋值 _progressRun）。
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        ShutdownCurrentProgressAndScene();

        var progressJson = SaveStorageFacade.ReadProgressJsonFromSnapshot(FileSystem, SaveRootPath, saveId);
        if (progressJson == null)
            throw new InvalidOperationException($"Missing required progress.json in save '{saveId}'.");
        var progressDict = Runtime.SndWorld.DeserializeTypedDataMap(progressJson);

        if (!progressDict.TryGetValue(WellKnownKeys.ActiveLevelId, out var levelData)
            || levelData.Data is not string activeLevelId
            || string.IsNullOrWhiteSpace(activeLevelId))
            throw new InvalidOperationException($"Cannot determine ActiveLevelId from progress in save '{saveId}'.");

        var payload = SaveStorageFacade.ReadSavePayloadFromSnapshot(FileSystem, SaveRootPath, saveId, activeLevelId);
        SaveStorageFacade.WriteSavePayloadToCurrent(FileSystem, SaveRootPath, payload);

        var progressRun = _runFactory.CreateProgressRun(saveId, activeLevelId, new Blackboard.Blackboard());
        _progressRun = progressRun;
        progressRun.LoadFromPayload(payload);
        SetActiveSaveState(saveId);
        return progressRun;
    }
}