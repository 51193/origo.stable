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
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sndWorld);
        Progress = progress;
        Session = session;
        SndWorld = sndWorld;

        _blackboardSerializer = new BlackboardJsonSerializer(SndWorld.JsonOptions);
        _sceneSerializer = new SndSceneJsonSerializer(SndWorld);
        _payloadFactory = new SaveGamePayloadFactory(Progress, Session, _blackboardSerializer, _sceneSerializer);
    }

    /// <summary>
    ///     流程级黑板，保存跨关卡的全局进度数据。
    /// </summary>
    public IBlackboard Progress { get; }

    /// <summary>
    ///     会话级黑板，保存当前关卡内的临时状态数据。
    /// </summary>
    public IBlackboard Session { get; }

    /// <summary>
    ///     当前 SND 世界实例，提供场景数据的注册与序列化支持。
    /// </summary>
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
        ArgumentNullException.ThrowIfNull(json);
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
        ArgumentNullException.ThrowIfNull(json);
        _blackboardSerializer.DeserializeInto(Session, json);
    }

    /// <summary>
    ///     将指定 SND 场景序列化为 JSON 字符串。
    /// </summary>
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

    /// <summary>
    ///     收集当前存档所需的全部数据，生成完整的 <see cref="SaveGamePayload"/>。
    /// </summary>
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