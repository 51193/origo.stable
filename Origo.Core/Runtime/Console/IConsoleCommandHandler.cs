using Origo.Core.Abstractions;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     单条控制台命令的手动注册处理器（非反射）。
/// </summary>
public interface IConsoleCommandHandler
{
    /// <summary>
    ///     命令名（首词），不区分大小写比较由路由器负责。
    /// </summary>
    string Name { get; }

    bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel, out string? errorMessage);
}