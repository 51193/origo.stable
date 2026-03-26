using System;
using Origo.Core.Abstractions;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     流程级运行时实现，持有流程黑板与当前会话实例（SessionRun）。
///     负责关卡切换编排与进度持久化，是 SessionRun 的生命周期所有者。
/// </summary>
public sealed partial class ProgressRun : IProgressRun
{
    private readonly RunFactory _factory;
    private SessionRun? _currentSession;
    private bool _disposed;

    public ProgressRun(
        RunFactory factory,
        RunStateScope progressScope,
        string saveId,
        string activeLevelId)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        ProgressScope = progressScope ?? throw new ArgumentNullException(nameof(progressScope));
        SaveId = saveId ?? throw new ArgumentNullException(nameof(saveId));
        ActiveLevelId = activeLevelId ?? throw new ArgumentNullException(nameof(activeLevelId));
        ProgressScope.Blackboard.Set(WellKnownKeys.ActiveLevelId, ActiveLevelId);
    }

    public RunStateScope ProgressScope { get; }

    public IBlackboard ProgressBlackboard => ProgressScope.Blackboard;

    public ISessionRun? CurrentSession => _currentSession;

    public string SaveId { get; private set; }

    public string ActiveLevelId { get; private set; }

    // Session creation / loading methods moved to ProgressRun.SessionLoading.cs

    public void SetSaveId(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));
        SaveId = saveId;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _currentSession?.Dispose();
        _currentSession = null;

        ProgressScope.StateMachines.PopAllOnQuit();
        ProgressScope.StateMachines.Clear();
        ProgressBlackboard.Clear();

        _disposed = true;
    }

    public void UpdateActiveLevel(string newLevelId)
    {
        if (string.IsNullOrWhiteSpace(newLevelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(newLevelId));
        ActiveLevelId = newLevelId;
        ProgressBlackboard.Set(WellKnownKeys.ActiveLevelId, newLevelId);
    }

    // PersistProgress moved to ProgressRun.Persistence.cs

    // SwitchLevel moved to ProgressRun.LevelSwitch.cs
}