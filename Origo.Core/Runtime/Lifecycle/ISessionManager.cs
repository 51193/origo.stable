using System.Collections.Generic;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     会话管理器接口，全权管理所有 <see cref="ISessionRun" /> 的生命周期。
///     <para>
///         职责包括：创建、持有、序列化/反序列化、销毁。
///         所有对 <see cref="ISessionRun" /> 的操作，要么通过此管理器进行，
///         要么通过 <see cref="ISessionRun" /> 自身的成员方法（黑板、场景、状态机）。
///     </para>
///     <para>
///         架构上不区分前台/后台会话。前台会话只是以 <see cref="ForegroundKey" />
///         为键挂载的普通会话，唯一的特殊之处在于至多一个。
///     </para>
/// </summary>
public interface ISessionManager
{
    /// <summary>前台会话在管理器中的保留键。</summary>
    const string ForegroundKey = "__foreground__";

    /// <summary>
    ///     当前前台会话；无活动前台会话时为 null。
    ///     等价于 <c>TryGet(ForegroundKey)</c>。
    /// </summary>
    ISessionRun? ForegroundSession { get; }

    /// <summary>获取所有已挂载会话的键列表（包含前台会话键，如果存在）。</summary>
    IReadOnlyCollection<string> Keys { get; }

    /// <summary>
    ///     按键获取会话。
    /// </summary>
    ISessionRun? TryGet(string key);

    /// <summary>
    ///     检查指定键的会话是否存在。
    /// </summary>
    bool Contains(string key);

    /// <summary>
    ///     创建后台关卡会话并自动挂载到管理器。
    ///     使用 <see cref="Snd.Scene.FullMemorySndSceneHost" /> 作为场景宿主，
    ///     与前台关卡共享策略池和进度黑板，但拥有独立的 SessionBlackboard、状态机和实体集合。
    /// </summary>
    /// <param name="key">会话标识键，不可为空或空白字符串，不可与已有键重复。</param>
    /// <param name="levelId">后台关卡标识符。</param>
    /// <param name="syncProcess">若为 true，该会话将参与 Process 帧更新。</param>
    /// <returns>已创建并挂载的后台会话。</returns>
    ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false);

    /// <summary>
    ///     创建后台关卡会话，从 <see cref="LevelPayload" /> 恢复状态，并自动挂载到管理器。
    ///     等价于 <see cref="CreateBackgroundSession" /> + 内部 LoadFromPayload。
    /// </summary>
    /// <param name="key">会话标识键。</param>
    /// <param name="levelId">后台关卡标识符。</param>
    /// <param name="payload">要恢复的关卡数据。</param>
    /// <param name="syncProcess">若为 true，该会话将参与 Process 帧更新。</param>
    /// <returns>已创建、恢复并挂载的后台会话。</returns>
    ISessionRun CreateBackgroundSessionFromPayload(string key, string levelId, LevelPayload payload,
        bool syncProcess = false);

    /// <summary>
    ///     销毁指定键的会话（Dispose 并从管理器移除）。
    ///     若键不存在则静默返回。
    /// </summary>
    /// <param name="key">会话标识键。</param>
    void DestroySession(string key);

    /// <summary>
    ///     对所有参与 Process 的后台会话执行帧更新。
    ///     前台会话由引擎适配层驱动，不在此方法范围内。
    /// </summary>
    /// <param name="delta">帧间隔时间（秒）。</param>
    void ProcessBackgroundSessions(double delta);
}
