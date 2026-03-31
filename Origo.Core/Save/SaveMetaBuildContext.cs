using System;
using Origo.Core.Abstractions;

namespace Origo.Core.Save;

/// <summary>
///     单次存档时传给 <see cref="ISaveMetaContributor" /> 的只读上下文（展示用 meta.map，与业务载荷序列化无关）。
/// </summary>
public readonly struct SaveMetaBuildContext
{
    public SaveMetaBuildContext(
        string saveId,
        string currentLevelId,
        IBlackboard progress,
        IBlackboard session,
        ISndSceneAccess sceneAccess)
    {
        ArgumentNullException.ThrowIfNull(saveId);
        ArgumentNullException.ThrowIfNull(currentLevelId);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sceneAccess);
        SaveId = saveId;
        CurrentLevelId = currentLevelId;
        Progress = progress;
        Session = session;
        SceneAccess = sceneAccess;
    }

    public string SaveId { get; }

    public string CurrentLevelId { get; }

    public IBlackboard Progress { get; }

    public IBlackboard Session { get; }

    public ISndSceneAccess SceneAccess { get; }
}
