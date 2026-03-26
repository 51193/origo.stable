using System;

namespace Origo.Core.Abstractions;

/// <summary>
///     抽象调度接口，由宿主环境驱动一帧或一周期的执行。
/// </summary>
public interface IScheduler
{
    /// <summary>
    ///     将一个动作安排在本帧或之后的某个阶段执行，具体策略由实现定义。
    /// </summary>
    /// <param name="action">要执行的行为。</param>
    void Enqueue(Action action);
}