using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.Core.Save;

/// <summary>
///     描述当前存档上下文（slot/save/level）并负责 Progress / Session 黑板与 SND 场景之间的转换。
///     不执行任何文件 I/O，所有输入输出均为 JSON 字符串或内存模型。
/// </summary>
public sealed class SaveContext
{
    private readonly BlackboardJsonSerializer _blackboardSerializer;
    private readonly SaveGamePayloadFactory _payloadFactory;
    private readonly SndSceneJsonSerializer _sceneSerializer;

    public SaveContext(
        IBlackboard progress,
        IBlackboard session,
        SndWorld sndWorld)
    {
        Progress = progress ?? throw new ArgumentNullException(nameof(progress));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        SndWorld = sndWorld ?? throw new ArgumentNullException(nameof(sndWorld));

        _blackboardSerializer = new BlackboardJsonSerializer(SndWorld.JsonOptions);
        _sceneSerializer = new SndSceneJsonSerializer(SndWorld);
        _payloadFactory = new SaveGamePayloadFactory(Progress, Session, SndWorld);
    }

    public IBlackboard Progress { get; }

    public IBlackboard Session { get; }

    public SndWorld SndWorld { get; }

    /// <summary>
    ///     将 Progress 黑板序列化为 JSON（使用 TypedData 保留类型信息）。
    /// </summary>
    public string SerializeProgress()
    {
        return _blackboardSerializer.Serialize(Progress);
    }

    /// <summary>
    ///     将 JSON 恢复到 Progress 黑板。
    /// </summary>
    public void DeserializeProgress(string json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        _blackboardSerializer.DeserializeInto(Progress, json);
    }

    /// <summary>
    ///     将 Session 黑板序列化为 JSON（使用 TypedData 保留类型信息）。
    /// </summary>
    public string SerializeSession()
    {
        return _blackboardSerializer.Serialize(Session);
    }

    /// <summary>
    ///     将 JSON 恢复到 Session 黑板。
    /// </summary>
    public void DeserializeSession(string json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        _blackboardSerializer.DeserializeInto(Session, json);
    }

    public string SerializeSndScene(ISndSceneAccess sceneAccess)
    {
        return _sceneSerializer.Serialize(sceneAccess);
    }

    /// <summary>
    ///     将 JSON 恢复到 SND 场景。
    ///     支持两种形式：完整的 SndMetaData 对象，或 { "sndName": "...", "templateKey": "..." } 简写，
    ///     与入口配置保持一致，统一通过 SndMappings.ResolveMetaListFromJsonArray 解析。
    /// </summary>
    public void DeserializeSndScene(ISndSceneAccess sceneAccess, string json, bool clearBeforeLoad = true)
    {
        _sceneSerializer.DeserializeInto(sceneAccess, json, clearBeforeLoad);
    }

    public SaveGamePayload SaveGame(
        ISndSceneAccess sceneAccess,
        string saveId,
        string currentLevelId,
        IReadOnlyDictionary<string, string>? customMeta = null,
        string progressStateMachinesJson = "",
        string sessionStateMachinesJson = "")
    {
        return _payloadFactory.Create(
            sceneAccess,
            saveId,
            currentLevelId,
            customMeta,
            progressStateMachinesJson,
            sessionStateMachinesJson);
    }
}