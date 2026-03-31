using System;
using System.Collections.Generic;
using System.Globalization;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;

namespace Origo.Core.Snd;

/// <summary>
///     Encapsulates save / load / continue game orchestration.
///     Extracted from the former SndContext.SaveFlow partial to keep SndContext focused.
/// </summary>
internal sealed class SaveGameWorkflow
{
    private readonly SndContext _ctx;

    internal SaveGameWorkflow(SndContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _ctx = ctx;
    }

    internal IReadOnlyList<string> ListSaves() =>
        SaveStorageFacade.EnumerateSaveIds(_ctx.FileSystem, _ctx.SaveRootPath);

    internal IReadOnlyList<SaveMetaDataEntry> ListSavesWithMetaData() =>
        SaveStorageFacade.EnumerateSavesWithMetaData(_ctx.FileSystem, _ctx.SaveRootPath);

    internal void RequestSaveGame(
        string newSaveId,
        string baseSaveId,
        IReadOnlyDictionary<string, string>? customMeta = null)
    {
        if (string.IsNullOrWhiteSpace(newSaveId))
            throw new ArgumentException("New save id cannot be null or whitespace.", nameof(newSaveId));
        if (string.IsNullOrWhiteSpace(baseSaveId))
            throw new ArgumentException("Base save id cannot be null or whitespace.", nameof(baseSaveId));

        _ctx.IncrementPendingPersistence();
        _ctx.EnqueueSystemDeferred(() =>
        {
            try
            {
                ExecuteSaveGameNow(newSaveId, baseSaveId, customMeta);
            }
            finally
            {
                _ctx.DecrementPendingPersistence();
            }
        });
    }

    internal void RequestLoadGame(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        _ctx.IncrementPendingPersistence();
        _ctx.EnqueueSystemDeferred(() =>
        {
            try
            {
                _ctx.Runtime.ResetConsoleState();
                _ctx.SetProgressRun(LoadOrContinueStrict(saveId));
            }
            finally
            {
                _ctx.DecrementPendingPersistence();
            }
        });
    }

    internal bool RequestContinueGame()
    {
        var saveId = _ctx.TryGetActiveSaveId();
        if (string.IsNullOrWhiteSpace(saveId))
            return false;

        RequestLoadGame(saveId);
        return true;
    }

    internal string RequestSaveGameAuto(
        string? newSaveId = null,
        IReadOnlyDictionary<string, string>? customMeta = null)
    {
        var baseSaveId = _ctx.TryGetActiveSaveId() ?? SndDefaults.InitialSaveId;
        var effectiveNewSaveId = string.IsNullOrWhiteSpace(newSaveId)
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
            : newSaveId;
        RequestSaveGame(effectiveNewSaveId, baseSaveId, customMeta);
        return effectiveNewSaveId;
    }

    internal bool HasContinueData()
    {
        var saveId = _ctx.TryGetActiveSaveId();
        return !string.IsNullOrWhiteSpace(saveId);
    }

    internal void SetContinueTarget(string saveId) => _ctx.SetActiveSaveState(saveId);

    internal void ClearContinueTarget() => _ctx.SystemBlackboard.Set(WellKnownKeys.ActiveSaveId, string.Empty);

    private void ExecuteSaveGameNow(
        string newSaveId,
        string baseSaveId,
        IReadOnlyDictionary<string, string>? customMeta)
    {
        _ctx.BeginWorkflow();
        try
        {
            var (progressRun, sessionRun) = _ctx.EnsureProgressAndSession();
            var currentLevelId = progressRun.ActiveLevelId;

            progressRun.UpdateActiveLevel(currentLevelId);

            var saveContext = new SaveContext(
                progressRun.ProgressBlackboard,
                sessionRun.SessionBlackboard,
                _ctx.Runtime.SndWorld);

            var metaContext = new SaveMetaBuildContext(
                newSaveId,
                currentLevelId,
                progressRun.ProgressBlackboard,
                sessionRun.SessionBlackboard,
                _ctx.Runtime.Snd.SceneHost);

            var mergedMeta = SaveMetaMerger.Merge(_ctx.SaveMetaContributors, in metaContext, customMeta);

            var jsonCodec = _ctx.Runtime.SndWorld.JsonCodec;
            var converterRegistry = _ctx.Runtime.SndWorld.ConverterRegistry;
            var progressSmJson =
                progressRun.ProgressScope.StateMachines.SerializeToDataSource(jsonCodec, converterRegistry);
            var sessionSmJson =
                sessionRun.SessionScope.StateMachines.SerializeToDataSource(jsonCodec, converterRegistry);

            var payload = saveContext.SaveGame(
                _ctx.Runtime.Snd.SceneHost,
                newSaveId,
                currentLevelId,
                mergedMeta,
                progressSmJson,
                sessionSmJson);

            SaveStorageFacade.WriteSavePayloadToCurrentThenSnapshot(
                _ctx.FileSystem,
                _ctx.SaveRootPath,
                payload,
                baseSaveId,
                newSaveId,
                _ctx.Runtime.Logger);

            progressRun.SetSaveId(newSaveId);
            _ctx.SetActiveSaveState(newSaveId);
        }
        finally
        {
            _ctx.EndWorkflow();
        }
    }

    internal IProgressRun LoadOrContinueStrict(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        _ctx.BeginWorkflow();
        try
        {
            _ctx.ShutdownCurrentProgressAndScene();

            var progressJson = SaveStorageFacade.ReadProgressJsonFromSnapshot(
                _ctx.FileSystem, _ctx.SaveRootPath, saveId);
            if (progressJson is null)
                throw new InvalidOperationException($"Missing required progress.json in save '{saveId}'.");
            var progressDict = _ctx.Runtime.SndWorld.DeserializeTypedDataMap(progressJson);

            if (!progressDict.TryGetValue(WellKnownKeys.ActiveLevelId, out var levelData)
                || levelData.Data is not string activeLevelId
                || string.IsNullOrWhiteSpace(activeLevelId))
                throw new InvalidOperationException(
                    $"Cannot determine ActiveLevelId from progress in save '{saveId}'.");

            var payload = SaveStorageFacade.ReadSavePayloadFromSnapshot(
                _ctx.FileSystem, _ctx.SaveRootPath, saveId, activeLevelId);
            SaveStorageFacade.WriteSavePayloadToCurrent(_ctx.FileSystem, _ctx.SaveRootPath, payload);

            var progressRun = _ctx.RunFactory.CreateProgressRun(
                saveId, activeLevelId, new Blackboard.Blackboard());
            _ctx.SetProgressRun(progressRun);
            progressRun.LoadFromPayload(payload);
            _ctx.SetActiveSaveState(saveId);
            return progressRun;
        }
        finally
        {
            _ctx.EndWorkflow();
        }
    }
}
