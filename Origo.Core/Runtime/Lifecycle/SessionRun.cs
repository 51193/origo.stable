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
    private readonly RunStateScope _sessionScope;
    private readonly ISndSceneAccess _sceneAccess;
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
        ArgumentNullException.ThrowIfNull(saveContext);
        ArgumentNullException.ThrowIfNull(levelId);
        ArgumentNullException.ThrowIfNull(sceneAccess);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(saveRootPath);
        _saveContext = saveContext;
        LevelId = levelId;
        _sceneAccess = sceneAccess;
        _fileSystem = fileSystem;
        _saveRootPath = saveRootPath;
        _sessionScope = new RunStateScope(sessionBlackboard, sessionStateMachines);
    }

    public RunStateScope SessionScope
    {
        get
        {
            ThrowIfDisposed();
            return _sessionScope;
        }
    }

    public IBlackboard SessionBlackboard
    {
        get
        {
            ThrowIfDisposed();
            return _sessionScope.Blackboard;
        }
    }

    public ISndSceneAccess SceneAccess
    {
        get
        {
            ThrowIfDisposed();
            return _sceneAccess;
        }
    }

    public string LevelId { get; }

    public void PersistLevelState()
    {
        ThrowIfDisposed();

        var levelPayload = new LevelPayload
        {
            LevelId = LevelId,
            SndSceneJson = _saveContext.SerializeSndScene(_sceneAccess),
            SessionJson = _saveContext.SerializeSession(),
            SessionStateMachinesJson = _sessionScope.StateMachines.SerializeToJson(_saveContext.SndWorld.JsonOptions)
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
        // Set flag first to prevent recursive Dispose calls (e.g. from cleanup callbacks).
        _disposed = true;

        _sessionScope.StateMachines.PopAllOnQuit();
        _sessionScope.StateMachines.Clear();
        _sceneAccess.ClearAll();
        _sessionScope.Blackboard.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}