using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     系统级运行时，持有系统黑板，负责加载或继续游戏流程。
/// </summary>
public interface ISystemRun
{
    IBlackboard SystemBlackboard { get; }

    /// <summary>
    ///     加载或继续游戏。saveId 为 null 时表示继续最近存档（SystemBlackboard 中的 active_save_id）。
    /// </summary>
    IProgressRun? LoadOrContinue(string? saveId);

    /// <summary>
    ///     将当前“继续/活动存档槽”写入系统黑板（单一入口，避免与 <see cref="SndContext" /> 多处重复维护）。
    /// </summary>
    void SetActiveSaveSlot(string saveId);
}