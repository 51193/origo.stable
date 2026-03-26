using System.Collections.Generic;

namespace Origo.Core.Save;

/// <summary>
///     单个关卡在存档中的表示，包含 SND 场景与 Session 黑板的序列化结果。
/// </summary>
public sealed class LevelPayload
{
    public string LevelId { get; set; } = string.Empty;

    public string SndSceneJson { get; set; } = string.Empty;

    public string SessionJson { get; set; } = string.Empty;

    /// <summary>
    ///     会话级字符串栈状态机快照 JSON（与 <c>session.json</c> 同目录的 <c>session_state_machines.json</c> 对应）。
    /// </summary>
    public string SessionStateMachinesJson { get; set; } = string.Empty;
}

/// <summary>
///     一次完整存档所需的数据包，仅包含与 Core 相关的 JSON 字符串。
///     具体文件布局与 I/O 由适配层决定。
/// </summary>
public sealed class SaveGamePayload
{
    public string SaveId { get; set; } = string.Empty;

    public string ActiveLevelId { get; set; } = string.Empty;

    public string ProgressJson { get; set; } = string.Empty;

    /// <summary>
    ///     流程级字符串栈状态机快照 JSON（与 <c>progress.json</c> 同目录的 <c>progress_state_machines.json</c> 对应）。
    /// </summary>
    public string ProgressStateMachinesJson { get; set; } = string.Empty;

    /// <summary>
    ///     可选的存档展示元数据（sidecar map 内容）。
    ///     不参与 ProgressBlackboard 语义，仅用于独立快速读取。
    /// </summary>
    public IReadOnlyDictionary<string, string>? CustomMeta { get; set; }

    public Dictionary<string, LevelPayload> Levels { get; set; } = new();
}