namespace Origo.Core.Abstractions;

/// <summary>
///     用于抽象时间来源，便于测试与跨引擎使用。
/// </summary>
public interface IClock
{
    /// <summary>
    ///     获取自某个固定参考点以来的总秒数。
    /// </summary>
    double TotalSeconds { get; }
}