using System;
using Origo.Core.Abstractions;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     关卡会话级运行时，持有会话黑板与 SND 场景访问。
/// </summary>
public interface ISessionRun : IDisposable
{
    RunStateScope SessionScope { get; }

    IBlackboard SessionBlackboard { get; }

    ISndSceneAccess SceneAccess { get; }

    string LevelId { get; }

    void PersistLevelState();
}