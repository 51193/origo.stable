using System;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    public void PersistProgress()
    {
        if (_currentSession is null)
            throw new InvalidOperationException("No active session.");

        var serializer = _factory.CreateSaveContext(
            ProgressBlackboard,
            _currentSession.SessionBlackboard);
        var progressJson = serializer.SerializeProgress();
        var smJson = ProgressScope.StateMachines.SerializeToJson(_factory.Runtime.SndWorld.JsonOptions);

        SavePayloadWriter.WriteProgressOnlyToCurrent(
            _factory.FileSystem,
            _factory.SaveRootPath,
            progressJson,
            smJson);
    }
}