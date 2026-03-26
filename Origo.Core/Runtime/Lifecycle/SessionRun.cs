using System;
using Origo.Core.Abstractions;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     关卡会话级运行时实现，持有关卡会话黑板与 SND 场景访问。
///     <para>
///         <see cref="Dispose" /> 边界：逻辑卸载（SessionBlackboard.Clear、SceneAccess.ClearAll）在 Dispose 内同步执行；
///         与 Godot 节点释放的时序由调用方保证（先 Dispose 完成逻辑卸载，再 Free 节点）。
///     </para>
/// </summary>
public sealed class SessionRun : ISessionRun
{
    private readonly IFileSystem _fileSystem;
    private readonly SaveContext _saveContext;
    private readonly string _saveRootPath;
    private bool _disposed;

    public SessionRun(
        SaveContext saveContext,
        string levelId,
        IBlackboard sessionBlackboard,
        ISndSceneAccess sceneAccess,
        StateMachineContainer sessionStateMachines,
        IFileSystem fileSystem,
        string saveRootPath)
    {
        _saveContext = saveContext ?? throw new ArgumentNullException(nameof(saveContext));
        LevelId = levelId ?? throw new ArgumentNullException(nameof(levelId));
        SceneAccess = sceneAccess ?? throw new ArgumentNullException(nameof(sceneAccess));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _saveRootPath = saveRootPath ?? throw new ArgumentNullException(nameof(saveRootPath));
        SessionScope = new RunStateScope(sessionBlackboard, sessionStateMachines);
    }

    public RunStateScope SessionScope { get; }

    public IBlackboard SessionBlackboard => SessionScope.Blackboard;

    public ISndSceneAccess SceneAccess { get; }

    public string LevelId { get; }

    public void PersistLevelState()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SessionRun));

        var levelPayload = new LevelPayload
        {
            LevelId = LevelId,
            SndSceneJson = _saveContext.SerializeSndScene(SceneAccess),
            SessionJson = _saveContext.SerializeSession(),
            SessionStateMachinesJson = SessionScope.StateMachines.ExportToJson(_saveContext.SndWorld.JsonOptions)
        };

        var currentRel = SavePathLayout.GetCurrentDirectory();
        SaveStorageFacade.WriteLevelPayloadOnly(
            _fileSystem,
            _saveRootPath,
            currentRel,
            levelPayload);
    }

    public void Dispose()
    {
        if (_disposed) return;

        SessionScope.StateMachines.PopAllOnQuit();
        SessionScope.StateMachines.Clear();
        SceneAccess.ClearAll();
        SessionBlackboard.Clear();
        _disposed = true;
    }
}