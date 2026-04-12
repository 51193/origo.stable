using Origo.Core.Save.Serialization;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    internal void PersistProgress()
    {
        var fgSession = ForegroundSession;
        if (fgSession is not null)
            SyncActiveLevelIdToProgress(fgSession.LevelId);

        var sessionBb = fgSession?.SessionBlackboard ?? new Blackboard.Blackboard();

        var serializer = new SaveContext(ProgressBlackboard, sessionBb, _progressRuntime.SndWorld);
        var progressJson = serializer.SerializeProgress();
        var smJson = ProgressScope.StateMachines.SerializeToDataSource(
            _progressRuntime.JsonCodec, _progressRuntime.ConverterRegistry);

        _progressRuntime.StorageService.WriteProgressOnlyToCurrent(progressJson, smJson);
    }
}
