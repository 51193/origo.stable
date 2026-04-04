using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Save;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     <see cref="ISessionManager" /> 的默认实现。
///     全权管理所有 <see cref="ISessionRun" /> 的生命周期：创建、持有、序列化/反序列化、销毁。
///     前台和后台会话均通过统一的内部 KVP 存储管理，
///     前台会话以 <see cref="ISessionManager.ForegroundKey" /> 为键挂载，无架构区别。
///     <para>
///         能力边界：SessionManager 负责 Session 的生命周期管理（创建/销毁/序列化），
///         Session 自身负责内部状态管理（黑板读写、场景操作、状态机）。
///         Session 的资源回收遵循 RAII 原则，由 Session 自身在 Dispose 中完成。
///     </para>
/// </summary>
internal sealed class SessionManager : ISessionManager
{
    private const string LogTag = "SessionManager";
    private readonly RunFactory _factory;
    private readonly IBlackboard _progressBlackboard;
    private readonly Dictionary<string, MountedSession> _sessions = new(StringComparer.Ordinal);
    private ILogger _logger = NullLogger.Instance;

    internal SessionManager(RunFactory factory, IBlackboard progressBlackboard)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(progressBlackboard);
        _factory = factory;
        _progressBlackboard = progressBlackboard;
    }

    /// <summary>获取所有参与 Process 帧更新的会话的键列表。</summary>
    internal IReadOnlyCollection<string> ProcessingKeys =>
        _sessions.Where(kvp => kvp.Value.SyncProcess)
            .Select(kvp => kvp.Key).ToArray();

    /// <inheritdoc />
    public ISessionRun? ForegroundSession =>
        _sessions.TryGetValue(ISessionManager.ForegroundKey, out var mounted) ? mounted.Session : null;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Keys => _sessions.Keys.ToArray();

    /// <inheritdoc />
    public ISessionRun? TryGet(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;
        return _sessions.TryGetValue(key, out var mounted) ? mounted.Session : null;
    }

    /// <inheritdoc />
    public bool Contains(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        return _sessions.ContainsKey(key);
    }

    /// <inheritdoc />
    public ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false)
    {
        ValidateKey(key);
        var session = _factory.CreateBackgroundSession(levelId);
        MountInternal(key, session, syncProcess);
        return session;
    }

    /// <inheritdoc />
    public ISessionRun CreateBackgroundSessionFromPayload(string key, string levelId, LevelPayload payload,
        bool syncProcess = false)
    {
        ValidateKey(key);
        var session = _factory.CreateBackgroundSessionFromPayload(levelId, payload);
        MountInternal(key, session, syncProcess);
        return session;
    }

    /// <inheritdoc />
    public void DestroySession(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (!_sessions.Remove(key, out var mounted))
            return;

        _logger.Log(LogLevel.Info, LogTag,
            $"Destroying session '{key}' (level: {mounted.Session.LevelId}).");

        // Clear mount tracking before Dispose to avoid re-entrant remove.
        if (mounted.Session is SessionRun sr)
        {
            sr.MountKey = null;
            sr.UnmountCallback = null;
        }

        mounted.Session.Dispose();
    }

    /// <inheritdoc />
    public void ProcessBackgroundSessions(double delta)
    {
        // Snapshot keys to allow modifications during iteration.
        var keys = _sessions.Keys.ToArray();
        foreach (var key in keys)
        {
            // Skip foreground session — it is driven by the engine adapter.
            if (string.Equals(key, ISessionManager.ForegroundKey, StringComparison.Ordinal))
                continue;
            if (!_sessions.TryGetValue(key, out var mounted) || !mounted.SyncProcess)
                continue;

            // FullMemorySndSceneHost supports ProcessAll for background sessions.
            if (mounted.Session.SceneHost is FullMemorySndSceneHost memHost)
                memHost.ProcessAll(delta);
        }
    }

    /// <summary>
    ///     注入日志实例，供生命周期事件输出。
    /// </summary>
    internal void SetLogger(ILogger logger) => _logger = logger;

    // ── Internal methods for ProgressRun ──────────────────────────────

    /// <summary>
    ///     创建前台会话并自动挂载。若已有前台会话，先销毁旧的。
    /// </summary>
    internal ISessionRun CreateForegroundSession(string levelId, ISndSceneHost sceneHost)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));
        ArgumentNullException.ThrowIfNull(sceneHost);

        var sessionBlackboard = new Blackboard.Blackboard();
        var saveContext = _factory.CreateSaveContext(_progressBlackboard, sessionBlackboard);
        var session = _factory.CreateSessionRun(saveContext, levelId, sessionBlackboard, sceneHost);
        MountInternal(ISessionManager.ForegroundKey, session, false);
        return session;
    }

    /// <summary>
    ///     创建前台会话，从 payload 恢复状态，并自动挂载。若已有前台会话，先销毁旧的。
    /// </summary>
    internal ISessionRun CreateForegroundFromPayload(string levelId, ISndSceneHost sceneHost, LevelPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var session = CreateForegroundSession(levelId, sceneHost);
        ((SessionRun)session).LoadFromPayload(payload);
        return session;
    }

    /// <summary>
    ///     销毁当前前台会话（若存在）。
    /// </summary>
    internal void DestroyForeground() => DestroySession(ISessionManager.ForegroundKey);

    /// <summary>
    ///     将指定键的会话序列化为 <see cref="LevelPayload" />。
    /// </summary>
    internal LevelPayload SerializeSession(string key)
    {
        if (!_sessions.TryGetValue(key, out var mounted))
            throw new InvalidOperationException($"No session with key '{key}' is mounted.");
        return ((SessionRun)mounted.Session).SerializeToPayload();
    }

    /// <summary>
    ///     将指定键的会话状态持久化到 current/ 目录。
    /// </summary>
    internal void PersistSession(string key)
    {
        if (!_sessions.TryGetValue(key, out var mounted))
            throw new InvalidOperationException($"No session with key '{key}' is mounted.");
        ((SessionRun)mounted.Session).PersistLevelState();
    }

    /// <summary>
    ///     从 <see cref="LevelPayload" /> 恢复指定键的会话状态。
    /// </summary>
    internal void LoadSessionFromPayload(string key, LevelPayload payload)
    {
        if (!_sessions.TryGetValue(key, out var mounted))
            throw new InvalidOperationException($"No session with key '{key}' is mounted.");
        ((SessionRun)mounted.Session).LoadFromPayload(payload);
    }

    /// <summary>
    ///     清除所有后台会话（Dispose 并移除）。前台会话不受影响。
    /// </summary>
    internal void ClearBackground()
    {
        var bgKeys = _sessions.Keys.Where(k =>
            !string.Equals(k, ISessionManager.ForegroundKey, StringComparison.Ordinal)).ToArray();
        foreach (var key in bgKeys)
            DestroySession(key);
    }

    /// <summary>
    ///     清除所有会话（Dispose 并移除）。
    /// </summary>
    internal void Clear()
    {
        var keys = _sessions.Keys.ToArray();
        foreach (var key in keys)
            DestroySession(key);
    }

    /// <summary>
    ///     获取指定键的会话是否参与 Process 帧更新。
    ///     若键不存在则返回 false。
    /// </summary>
    internal bool GetSyncProcess(string key) =>
        !string.IsNullOrWhiteSpace(key) && _sessions.TryGetValue(key, out var mounted) && mounted.SyncProcess;

    /// <summary>
    ///     获取所有已挂载的后台会话（不含前台）。
    /// </summary>
    internal IReadOnlyList<KeyValuePair<string, ISessionRun>> GetBackgroundSessions() =>
        _sessions
            .Where(kvp => !string.Equals(kvp.Key, ISessionManager.ForegroundKey, StringComparison.Ordinal))
            .Select(kvp => new KeyValuePair<string, ISessionRun>(kvp.Key, kvp.Value.Session))
            .ToArray();

    /// <summary>
    ///     将所有后台会话序列化为 LevelPayload 字典（key → LevelPayload）。
    /// </summary>
    internal IReadOnlyDictionary<string, LevelPayload> SerializeBackgroundSessions()
    {
        var result = new Dictionary<string, LevelPayload>();
        foreach (var kvp in GetBackgroundSessions())
        {
            var payload = ((SessionRun)kvp.Value).SerializeToPayload();
            result[kvp.Key] = payload;
        }

        return result;
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Session key cannot be null or whitespace.", nameof(key));
        if (_sessions.ContainsKey(key))
            throw new InvalidOperationException($"A session with key '{key}' is already mounted.");
    }

    private void MountInternal(string key, ISessionRun session, bool syncProcess)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Session key cannot be null or whitespace.", nameof(key));

        // If mounting foreground and old foreground exists, destroy old first.
        if (string.Equals(key, ISessionManager.ForegroundKey, StringComparison.Ordinal)
            && _sessions.ContainsKey(key))
            DestroyForeground();

        if (_sessions.ContainsKey(key))
            throw new InvalidOperationException($"A session with key '{key}' is already mounted.");

        _sessions[key] = new MountedSession(session, syncProcess);
        _logger.Log(LogLevel.Info, LogTag,
            $"Mounted session '{key}' (level: {session.LevelId}, syncProcess: {syncProcess}).");

        // Track mount key on the session for auto-unmount on Dispose.
        if (session is SessionRun sr)
        {
            sr.MountKey = key;
            sr.UnmountCallback = run =>
            {
                if (run.MountKey is not null && _sessions.ContainsKey(run.MountKey))
                    _sessions.Remove(run.MountKey);
            };
        }
    }

    private sealed record MountedSession(ISessionRun Session, bool SyncProcess);
}
