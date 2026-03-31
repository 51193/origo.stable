using System;
using Origo.Core.Abstractions;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     流程级运行时，持有流程黑板与当前关卡会话运行实例。
/// </summary>
public interface IProgressRun : IDisposable
{
    RunStateScope ProgressScope { get; }

    IBlackboard ProgressBlackboard { get; }

    ISessionRun? CurrentSession { get; }

    string SaveId { get; }

    string ActiveLevelId { get; }

    /// <summary>
    ///     从当前 ActiveLevelId 创建 SessionRun。
    /// </summary>
    ISessionRun? CreateFromActiveLevel();

    /// <summary>
    ///     当场景已经加载完成时，创建并绑定 SessionRun，不重复清场和反序列化。
    /// </summary>
    ISessionRun? CreateFromAlreadyLoadedScene();

    /// <summary>
    ///     从完整 payload 恢复流程与关卡会话状态。
    /// </summary>
    void LoadFromPayload(SaveGamePayload payload);

    /// <summary>
    ///     更新当前激活关卡 ID。
    /// </summary>
    void UpdateActiveLevel(string newLevelId);

    /// <summary>
    ///     切换关卡。严格按顺序：
    ///     PersistLevelState → UpdateActiveLevel → PersistProgress → Dispose → LoadSessionRunFromCurrent。
    ///     <para>
    ///         strict 语义：任一步失败均直接抛异常；调用方应将其视为致命（early stage let-it-crash），
    ///         不保证保留旧会话或回滚磁盘状态。
    ///     </para>
    /// </summary>
    void SwitchLevel(string newLevelId);

    /// <summary>
    ///     持久化流程黑板到 current 目录的 progress.json。
    /// </summary>
    void PersistProgress();

    /// <summary>
    ///     更新当前 saveId（例如 SaveGameAuto 生成新存档后）。
    /// </summary>
    void SetSaveId(string saveId);
}
