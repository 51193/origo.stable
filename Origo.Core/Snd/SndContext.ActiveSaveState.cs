using System;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;

namespace Origo.Core.Snd;

public sealed partial class SndContext
{
    private string? TryGetActiveSaveId()
    {
        var (found, value) = SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        return found && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    private void SetActiveSaveState(string saveId)
    {
        _systemRun.SetActiveSaveSlot(saveId);
    }

    private IProgressRun EnsureProgressRun()
    {
        return _progressRun ?? throw new InvalidOperationException(
            "No active ProgressRun. Call RequestLoadGame/RequestContinueGame/RequestLoadInitialSave/RequestLoadMainMenuEntrySave first.");
    }

    private (IProgressRun progressRun, ISessionRun sessionRun) EnsureProgressAndSession()
    {
        var progressRun = EnsureProgressRun();
        var sessionRun = progressRun.CurrentSession ?? throw new InvalidOperationException(
            "No active SessionRun. Current progress run has not created a session instance.");
        return (progressRun, sessionRun);
    }
}