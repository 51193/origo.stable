using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    public void PersistProgress()
    {
        var serializer = _factory.CreateSaveContext(
            ProgressBlackboard,
            _currentSession?.SessionBlackboard ?? new Blackboard.Blackboard());
        var progressJson = serializer.SerializeProgress();
        var smJson = ProgressScope.StateMachines.ExportToJson(_factory.Runtime.SndWorld.JsonOptions);

        SavePayloadWriter.WriteProgressOnlyToCurrent(
            _factory.FileSystem,
            _factory.SaveRootPath,
            progressJson,
            smJson);
    }
}