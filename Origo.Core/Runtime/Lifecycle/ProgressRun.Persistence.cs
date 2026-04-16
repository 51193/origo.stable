using Origo.Core.Save.Serialization;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    internal void PersistProgress()
    {
        var fgSession = ForegroundSession;
        if (fgSession is not null)
            SyncForegroundTopologyToProgress(fgSession.LevelId);

        var sessionBb = fgSession?.SessionBlackboard ?? new Blackboard.Blackboard();

        var serializer = new SaveContext(ProgressBlackboard, sessionBb, _progressRuntime.SndWorld);
        var progressNode = serializer.SerializeProgress();
        var smNode = ProgressScope.StateMachines.SerializeToNode(_progressRuntime.ConverterRegistry);

        _progressRuntime.StorageService.WriteProgressOnlyToCurrent(progressNode, smNode);
    }
}
