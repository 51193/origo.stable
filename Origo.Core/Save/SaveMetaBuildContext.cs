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
        SaveId = saveId ?? throw new ArgumentNullException(nameof(saveId));
        CurrentLevelId = currentLevelId ?? throw new ArgumentNullException(nameof(currentLevelId));
        Progress = progress ?? throw new ArgumentNullException(nameof(progress));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        SceneAccess = sceneAccess ?? throw new ArgumentNullException(nameof(sceneAccess));
    }

    public string SaveId { get; }

    public string CurrentLevelId { get; }

    public IBlackboard Progress { get; }

    public IBlackboard Session { get; }

    public ISndSceneAccess SceneAccess { get; }
}