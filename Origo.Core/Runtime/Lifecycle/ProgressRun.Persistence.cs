namespace Origo.Core.Runtime.Lifecycle;

public sealed partial class ProgressRun
{
    /// <summary>
    ///     持久化流程级状态（流程黑板 + 状态机）到 current/ 目录。
    ///     不依赖前台会话——仅序列化流程级数据。
    /// </summary>
    internal void PersistProgress()
    {
        var fgSession = ForegroundSession;
        if (fgSession is not null)
            SyncActiveLevelIdToProgress(fgSession.LevelId);

        var sessionBb = fgSession?.SessionBlackboard ?? new Blackboard.Blackboard();

        var serializer = _factory.CreateSaveContext(ProgressBlackboard, sessionBb);
        var progressJson = serializer.SerializeProgress();
        var smJson = ProgressScope.StateMachines.SerializeToDataSource(
            _factory.Runtime.SndWorld.JsonCodec,
            _factory.Runtime.SndWorld.ConverterRegistry);

        _factory.StorageService.WriteProgressOnlyToCurrent(progressJson, smJson);
    }
}
