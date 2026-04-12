using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd;

/// <summary>
///     用于纯运行时单测场景的空上下文实现。
/// </summary>
internal sealed class NullSndContext : ISndContext
{
    internal static readonly NullSndContext Instance = new();
    private static readonly IBlackboard EmptyBlackboard = new Blackboard.Blackboard();

    private NullSndContext()
    {
    }

    public IBlackboard SystemBlackboard => EmptyBlackboard;
    public IBlackboard? ProgressBlackboard => null;
    public ISessionManager SessionManager => EmptySessionManager.Instance;
    public ISessionRun? CurrentSession => null;
    public bool IsFrontSession => false;
    public void EnqueueBusinessDeferred(Action action) => action();
    public void FlushDeferredActionsForCurrentFrame()
    {
    }

    public int GetPendingPersistenceRequestCount() => 0;
    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null) =>
        throw new InvalidOperationException("NullSndContext does not support templates.");
    public bool TrySubmitConsoleCommand(string commandLine) => false;
    public void ProcessConsolePending()
    {
    }

    public long SubscribeConsoleOutput(Action<string> onLine) => 0;
    public void UnsubscribeConsoleOutput(long subscriptionId)
    {
    }

    public StateMachineContainer? GetProgressStateMachines() => null;
    public IReadOnlyList<string> ListSaves() => Array.Empty<string>();
    public void RequestLoadGame(string saveId)
    {
    }

    public void RequestSaveGame(string newSaveId)
    {
    }

    public string RequestSaveGameAuto(string? newSaveId = null) => newSaveId ?? string.Empty;
    public void SetContinueTarget(string saveId)
    {
    }

    public void RequestSwitchForegroundLevel(string newLevelId)
    {
    }

    public bool HasContinueData() => false;
    public bool RequestContinueGame() => false;
    public void RequestLoadInitialSave()
    {
    }
    public void RequestLoadMainMenuEntrySave()
    {
    }
}
