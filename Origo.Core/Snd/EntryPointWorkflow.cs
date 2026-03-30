using System;
using Origo.Core.Runtime;
using Origo.Core.Save;

namespace Origo.Core.Snd;

/// <summary>
///     Encapsulates initial-save / main-menu-entry / change-level orchestration.
///     Extracted from the former SndContext.Entry partial to keep SndContext focused.
/// </summary>
internal sealed class EntryPointWorkflow
{
    private readonly SndContext _ctx;

    internal EntryPointWorkflow(SndContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _ctx = ctx;
    }

    internal void RequestLoadInitialSave()
    {
        _ctx.EnqueueSystemDeferred(ExecuteLoadInitialSaveNow);
    }

    internal void RequestLoadMainMenuEntrySave()
    {
        _ctx.EnqueueSystemDeferred(ExecuteLoadMainMenuEntrySaveNow);
    }

    private void ExecuteLoadInitialSaveNow()
    {
        _ctx.BeginWorkflow();
        try
        {
            _ctx.Runtime.ResetConsoleState();

            _ctx.ShutdownCurrentProgressAndScene();

            var payload = SaveStorageFacade.ReadSavePayloadFromSnapshot(
                _ctx.FileSystem,
                _ctx.InitialSaveRootPath,
                SndDefaults.InitialSaveId,
                SndDefaults.InitialLevelId);

            payload.SaveId = SndDefaults.InitialSaveId;

            SaveStorageFacade.WriteSavePayloadToCurrent(_ctx.FileSystem, _ctx.SaveRootPath, payload);
            var progressRun = _ctx.RunFactory.CreateProgressRun(
                SndDefaults.InitialSaveId,
                payload.ActiveLevelId,
                new Blackboard.Blackboard());
            // Must bind ProgressRun before loading payload so entity AfterLoad can access session state machines.
            _ctx.SetProgressRun(progressRun);
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

            var saveId = _ctx.TryGetActiveSaveId() ?? SndDefaults.InitialSaveId;
            var mainMenuProgressRun = _ctx.RunFactory.CreateProgressRun(
                saveId,
                SndDefaults.MainMenuLevelId,
                new Blackboard.Blackboard());
            mainMenuProgressRun.CreateFromAlreadyLoadedScene();
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

    internal void RequestChangeLevel(string newLevelId)
    {
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException("New level id cannot be null or whitespace.", nameof(newLevelId));

        _ctx.EnqueueBusinessDeferred(() => _ctx.EnsureProgressRun().SwitchLevel(newLevelId));
    }
}
