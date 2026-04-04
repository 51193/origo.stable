using System;
using Origo.Core.Abstractions;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Serialization;
using Origo.Core.Save.Storage;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     关卡会话级运行时实现，持有关卡会话黑板与 SND 场景访问。
///     前台和后台关卡均为同一类型，区别仅在于注入的 <see cref="ISndSceneHost" /> 实现：
///     前台注入引擎适配层宿主，后台注入 <see cref="Snd.Scene.FullMemorySndSceneHost" />。
///     两者返回同一接口 <see cref="ISndSceneHost" />，业务层无需按宿主类型分叉逻辑。
///     通过注入 <see cref="ISaveStorageService" /> 与外围存储系统解耦，
///     确保可在不同运行模式间平滑替换。
///     <para>
///         能力边界：SessionRun 拥有对自身内部的完全支配权（黑板读写、场景操作、状态机），
///         但生命周期（创建 / 销毁 / 序列化）由 <see cref="SessionManager" /> 管理。
///         资源回收遵循 RAII 原则：SessionRun 在 Dispose 中回收自己的所有资源。
///     </para>
///     <para>
///         <see cref="Dispose" /> 边界：逻辑卸载（SessionBlackboard.Clear、SceneHost.ClearAll）在 Dispose 内同步执行；
///         与 Godot 节点释放的时序由调用方保证（先 Dispose 完成逻辑卸载，再 Free 节点）。
///     </para>
/// </summary>
public sealed class SessionRun : ISessionRun
{
    private const string LogTag = "SessionRun";
    private readonly ILogger _logger;
    private readonly SaveContext _saveContext;
    private readonly ISndSceneHost _sceneHost;
    private readonly RunStateScope _sessionScope;
    private readonly ISaveStorageService _storageService;
    private bool _disposed;

    internal SessionRun(
        SaveContext saveContext,
        string levelId,
        IBlackboard sessionBlackboard,
        ISndSceneHost sceneHost,
        StateMachineContainer sessionStateMachines,
        ISaveStorageService storageService,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(saveContext);
        ArgumentNullException.ThrowIfNull(levelId);
        ArgumentNullException.ThrowIfNull(sceneHost);
        ArgumentNullException.ThrowIfNull(storageService);
        _saveContext = saveContext;
        LevelId = levelId;
        _sceneHost = sceneHost;
        _storageService = storageService;
        _logger = logger ?? NullLogger.Instance;
        _sessionScope = new RunStateScope(sessionBlackboard, sessionStateMachines);
        _logger.Log(LogLevel.Info, LogTag, $"Created SessionRun for level '{levelId}'.");
    }

    /// <summary>
    ///     内部使用的完整 RunStateScope，包含黑板与状态机。
    ///     不在 <see cref="ISessionRun" /> 接口上暴露，仅供 ProgressRun / RunFactory 等内部代码使用。
    /// </summary>
    internal RunStateScope SessionScope
    {
        get
        {
            ThrowIfDisposed();
            return _sessionScope;
        }
    }

    /// <summary>
    ///     当前会话在 <see cref="ISessionManager" /> 中的挂载键；未挂载时为 null。
    ///     由 SessionManager 在创建/销毁时自动维护，外部不应直接设置。
    /// </summary>
    internal string? MountKey { get; set; }

    /// <summary>
    ///     在 SessionRun 被 Dispose 时需要自动从 SessionManager 卸载的回调。
    ///     由 SessionManager 在创建时注入。
    /// </summary>
    internal Action<SessionRun>? UnmountCallback { get; set; }

    public IBlackboard SessionBlackboard
    {
        get
        {
            ThrowIfDisposed();
            return _sessionScope.Blackboard;
        }
    }

    public ISndSceneHost SceneHost
    {
        get
        {
            ThrowIfDisposed();
            return _sceneHost;
        }
    }

    public string LevelId { get; }

    /// <inheritdoc />
    public StateMachineContainer GetSessionStateMachines()
    {
        ThrowIfDisposed();
        return _sessionScope.StateMachines;
    }

    public void Dispose()
    {
        if (_disposed) return;
        // Set flag first to prevent recursive Dispose calls (e.g. from cleanup callbacks).
        _disposed = true;
        _logger.Log(LogLevel.Info, LogTag,
            $"Disposing SessionRun for level '{LevelId}' (mount key: {MountKey ?? "none"}).");

        // Auto-persist before cleanup: save current session state so no runtime data is lost.
        try
        {
            PersistLevelStateInternal();
        }
        catch (Exception ex)
        {
            // Best-effort: if persistence fails (e.g. no storage configured), log warning and continue.
            _logger.Log(LogLevel.Warning, LogTag,
                $"Auto-persist failed during Dispose for level '{LevelId}': {ex.Message}");
        }

        // Auto-unmount from SessionManager if still mounted.
        UnmountCallback?.Invoke(this);
        MountKey = null;
        UnmountCallback = null;

        _sessionScope.StateMachines.PopAllOnQuit();
        _sessionScope.StateMachines.Clear();
        _sceneHost.ClearAll();
        _sessionScope.Blackboard.Clear();
    }

    /// <summary>
    ///     将当前会话状态序列化为 <see cref="LevelPayload" />（不执行任何磁盘 I/O）。
    ///     仅由 <see cref="SessionManager" /> 调用，不在 <see cref="ISessionRun" /> 接口暴露。
    /// </summary>
    internal LevelPayload SerializeToPayload()
    {
        ThrowIfDisposed();

        return new LevelPayload
        {
            LevelId = LevelId,
            SndSceneJson = _saveContext.SerializeSndScene(_sceneHost),
            SessionJson = _saveContext.SerializeSession(),
            SessionStateMachinesJson =
                _sessionScope.StateMachines.SerializeToDataSource(_saveContext.SndWorld.JsonCodec,
                    _saveContext.SndWorld.ConverterRegistry)
        };
    }

    /// <summary>
    ///     从 <see cref="LevelPayload" /> 恢复会话状态（黑板、状态机、场景实体）。
    ///     仅由 <see cref="SessionManager" /> 调用，不在 <see cref="ISessionRun" /> 接口暴露。
    /// </summary>
    internal void LoadFromPayload(LevelPayload payload)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(payload);
        _logger.Log(LogLevel.Info, LogTag, $"Loading payload for level '{LevelId}'.");

        // 1. 恢复会话黑板
        if (!string.IsNullOrWhiteSpace(payload.SessionJson))
            _saveContext.DeserializeSession(payload.SessionJson);

        // 2. 恢复状态机（不触发钩子，等场景加载完毕后统一 Flush）
        if (!string.IsNullOrWhiteSpace(payload.SessionStateMachinesJson))
            _sessionScope.StateMachines.DeserializeWithoutHooks(
                payload.SessionStateMachinesJson,
                _saveContext.SndWorld.JsonCodec,
                _saveContext.SndWorld.ConverterRegistry);

        // 3. 恢复 SND 场景实体
        if (!string.IsNullOrWhiteSpace(payload.SndSceneJson))
            _saveContext.DeserializeSndScene(_sceneHost, payload.SndSceneJson);

        // 4. 统一触发 AfterLoad 钩子
        _sessionScope.StateMachines.FlushAllAfterLoad();
    }

    /// <summary>
    ///     将会话状态序列化并持久化到 current/ 目录。
    ///     仅由 <see cref="SessionManager" /> 调用，不在 <see cref="ISessionRun" /> 接口暴露。
    /// </summary>
    internal void PersistLevelState()
    {
        ThrowIfDisposed();
        _logger.Log(LogLevel.Info, LogTag, $"Persisting level state for '{LevelId}'.");

        var levelPayload = SerializeToPayload();

        _storageService.WriteLevelPayloadOnlyToCurrent(levelPayload);
    }

    /// <summary>
    ///     Internal persistence that does not check disposed flag (called from Dispose).
    /// </summary>
    private void PersistLevelStateInternal()
    {
        var levelPayload = new LevelPayload
        {
            LevelId = LevelId,
            SndSceneJson = _saveContext.SerializeSndScene(_sceneHost),
            SessionJson = _saveContext.SerializeSession(),
            SessionStateMachinesJson =
                _sessionScope.StateMachines.SerializeToDataSource(_saveContext.SndWorld.JsonCodec,
                    _saveContext.SndWorld.ConverterRegistry)
        };

        _storageService.WriteLevelPayloadOnlyToCurrent(levelPayload);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
