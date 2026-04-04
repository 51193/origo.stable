using System;
using Origo.Core.Runtime;
using Origo.Core.Save.Storage;

namespace Origo.Core.Snd.Workflow;

/// <summary>
///     Encapsulates initial-save / main-menu-entry / change-level orchestration.
///     Extracted from the former SndContext.Entry partial to keep SndContext focused.
///     使用 <see cref="ISaveStorageService" /> 替代静态 <see cref="SaveStorageFacade" /> 调用，
///     使用 <see cref="ISessionDefaultsProvider" /> 替代硬编码 <see cref="SndDefaults" />。
/// </summary>
internal sealed class EntryPointWorkflow
{
    private readonly SndContext _ctx;

    internal EntryPointWorkflow(SndContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _ctx = ctx;
    }

    private ISaveStorageService Storage => _ctx.StorageService;
    private ISaveStorageService InitialStorage => _ctx.InitialStorageService;
    private ISessionDefaultsProvider Defaults => _ctx.DefaultsProvider;

    internal void RequestLoadInitialSave() => _ctx.EnqueueSystemDeferred(ExecuteLoadInitialSaveNow);

    internal void RequestLoadMainMenuEntrySave() => _ctx.EnqueueSystemDeferred(ExecuteLoadMainMenuEntrySaveNow);

    private void ExecuteLoadInitialSaveNow()
    {
        _ctx.BeginWorkflow();
        try
        {
            _ctx.Runtime.ResetConsoleState();

            _ctx.ShutdownCurrentProgressAndScene();

            var payload = InitialStorage.ReadSavePayloadFromSnapshot(
                Defaults.InitialSaveId,
                Defaults.InitialLevelId);

            payload.SaveId = Defaults.InitialSaveId;

            Storage.DeleteCurrentDirectory();
            Storage.WriteSavePayloadToCurrent(payload);
            var progressRun = _ctx.RunFactory.CreateProgressRun(
                Defaults.InitialSaveId,
                new Blackboard.Blackboard());
            _ctx.SetProgressRun(progressRun);
            // LoadFromPayload mounts foreground and ensures WellKnownKeys.ActiveLevelId on the progress blackboard.
            progressRun.LoadFromPayload(payload);
            _ctx.ClearContinueTarget();
        }
        finally
        {
            _ctx.EndWorkflow();
        }
    }

    private void ExecuteLoadMainMenuEntrySaveNow()
    {
        _ctx.BeginWorkflow();
        try
        {
            _ctx.Runtime.ResetConsoleState();

            _ctx.ShutdownCurrentProgressAndScene();

            var saveId = _ctx.TryGetActiveSaveId() ?? Defaults.InitialSaveId;
            var mainMenuProgressRun = _ctx.RunFactory.CreateProgressRun(
                saveId,
                new Blackboard.Blackboard());
            // LoadAndMountForeground syncs WellKnownKeys.ActiveLevelId for subsequent saves / persistence.
            mainMenuProgressRun.LoadAndMountForeground(Defaults.MainMenuLevelId);
            _ctx.SetProgressRun(mainMenuProgressRun);

            OrigoAutoInitializer.LoadAndSpawnFromFile(
                _ctx.EntryConfigPath,
                _ctx.Runtime.Snd,
                _ctx.FileSystem,
                _ctx.Runtime.Logger);
        }
        finally
        {
            _ctx.EndWorkflow();
        }
    }

    internal void RequestSwitchForegroundLevel(string newLevelId)
    {
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException("New level id cannot be null or whitespace.", nameof(newLevelId));

        _ctx.EnqueueBusinessDeferred(() =>
        {
            _ctx.EnsureProgressRun().SwitchForeground(newLevelId);
        });
    }
}
