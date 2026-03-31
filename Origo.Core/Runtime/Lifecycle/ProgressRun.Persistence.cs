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
        var smJson = ProgressScope.StateMachines.SerializeToDataSource(_factory.Runtime.SndWorld.JsonCodec,
            _factory.Runtime.SndWorld.ConverterRegistry);

        SavePayloadWriter.WriteProgressOnlyToCurrent(
            _factory.FileSystem,
            _factory.SaveRootPath,
            progressJson,
            smJson);
    }
}
