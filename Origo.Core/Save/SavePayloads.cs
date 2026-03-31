using System.Collections.Generic;

namespace Origo.Core.Save;

/// <summary>
///     单个关卡在存档中的表示，包含 SND 场景与 Session 黑板的序列化结果。
/// </summary>
public sealed class LevelPayload
{
    /// <summary>
    ///     关卡唯一标识符。
    /// </summary>
    public string LevelId { get; set; } = string.Empty;

    /// <summary>
    ///     该关卡 SND 场景的序列化 JSON。
    /// </summary>
    public string SndSceneJson { get; set; } = string.Empty;

    /// <summary>
    ///     该关卡 Session 黑板的序列化 JSON。
    /// </summary>
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
    /// <summary>
    ///     当前存档格式版本号。变更时递增，用于启动时校验格式兼容性。
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    ///     存档格式版本号，用于加载时校验兼容性。
    /// </summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>
    ///     存档槽唯一标识符。
    /// </summary>
    public string SaveId { get; set; } = string.Empty;

    /// <summary>
    ///     当前活跃关卡的标识符。
    /// </summary>
    public string ActiveLevelId { get; set; } = string.Empty;

    /// <summary>
    ///     流程级黑板的序列化 JSON。
    /// </summary>
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

    /// <summary>
    ///     按关卡 ID 索引的所有关卡存档数据。
    /// </summary>
    public Dictionary<string, LevelPayload> Levels { get; set; } = new();
}
