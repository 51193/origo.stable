namespace Origo.Core.Snd;

/// <summary>
///     会话默认值提供者接口。前台和后台统一使用同一契约获取初始存档/关卡 ID 等默认值，
///     避免硬编码常量散布在业务代码中，支持按环境注入不同策略。
/// </summary>
public interface ISessionDefaultsProvider
{
    /// <summary>初始存档 ID。</summary>
    string InitialSaveId { get; }

    /// <summary>初始关卡 ID。</summary>
    string InitialLevelId { get; }

    /// <summary>主菜单关卡 ID。</summary>
    string MainMenuLevelId { get; }
}
