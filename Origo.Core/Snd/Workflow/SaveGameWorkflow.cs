using System;
using System.Collections.Generic;
using System.Globalization;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Storage;

namespace Origo.Core.Snd.Workflow;

/// <summary>
///     Encapsulates save / load / continue game orchestration.
///     Extracted from the former SndContext.SaveFlow partial to keep SndContext focused.
///     使用 <see cref="ISaveStorageService" /> 替代静态 <see cref="SaveStorageFacade" /> 调用，
///     使用 <see cref="ISessionDefaultsProvider" /> 替代硬编码 <see cref="SndDefaults" />。
/// </summary>
internal sealed class SaveGameWorkflow
{
    private readonly SndContext _ctx;

    internal SaveGameWorkflow(SndContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _ctx = ctx;
    }

    private ISaveStorageService Storage => _ctx.StorageService;
    private ISessionDefaultsProvider Defaults => _ctx.DefaultsProvider;

    internal IReadOnlyList<string> ListSaves() =>
        Storage.EnumerateSaveIds();

    internal IReadOnlyList<SaveMetaDataEntry> ListSavesWithMetaData() =>
        Storage.EnumerateSavesWithMetaData();

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
        var baseSaveId = _ctx.TryGetActiveSaveId() ?? Defaults.InitialSaveId;
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
            var progressRun = _ctx.EnsureProgressRun();
            _ = progressRun.ForegroundSession ?? throw new InvalidOperationException(
                "No active foreground SessionRun. Current progress run has no foreground session mounted.");

            var metaContext = progressRun.BuildSaveMetaContext(newSaveId);
            var mergedMeta = SaveMetaMerger.Merge(_ctx.SaveMetaContributors, in metaContext, customMeta);

            var payload = progressRun.BuildSavePayload(newSaveId, mergedMeta);

            Storage.WriteSavePayloadToCurrentThenSnapshot(
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

    internal ProgressRun LoadOrContinueStrict(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        _ctx.BeginWorkflow();
        try
        {
            _ctx.ShutdownCurrentProgressAndScene();

            var progressJson = Storage.ReadProgressJsonFromSnapshot(saveId);
            if (progressJson is null)
                throw new InvalidOperationException($"Missing required progress.json in save '{saveId}'.");
            var progressDict = _ctx.Runtime.SndWorld.DeserializeTypedDataMap(progressJson);

            if (!progressDict.TryGetValue(WellKnownKeys.ActiveLevelId, out var levelData)
                || levelData.Data is not string activeLevelId
                || string.IsNullOrWhiteSpace(activeLevelId))
                throw new InvalidOperationException(
                    $"Cannot determine ActiveLevelId from progress in save '{saveId}'.");

            var payload = Storage.ReadSavePayloadFromSnapshot(saveId, activeLevelId);
            Storage.DeleteCurrentDirectory();
            Storage.WriteSavePayloadToCurrent(payload);

            var progressRun = _ctx.RunFactory.CreateProgressRun(
                saveId, new Blackboard.Blackboard());
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
