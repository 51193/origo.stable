using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     基于字典的手动命令路由。
/// </summary>
public sealed class ConsoleCommandRouter
{
    private readonly Dictionary<string, IConsoleCommandHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(IConsoleCommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (string.IsNullOrWhiteSpace(handler.Name))
            throw new ArgumentException("Handler name cannot be empty.", nameof(handler));

        _handlers[handler.Name] = handler;
    }

    public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(outputChannel);

        if (!_handlers.TryGetValue(invocation.Command, out var handler))
        {
            errorMessage = $"Unknown command '{invocation.Command}'.";
            return false;
        }

        return handler.TryExecute(invocation, outputChannel, out errorMessage);
    }
}