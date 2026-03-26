using System;
using Origo.Core.Abstractions;

namespace Origo.Core.Runtime.Console.CommandImpl;

public class SndCountCommandHandler : IConsoleCommandHandler
{
    private readonly OrigoRuntime _runtime;

    public SndCountCommandHandler(OrigoRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string Name => "snd_count";

    public bool TryExecute(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(outputChannel);

        var count = _runtime.Snd.GetEntities().Count;
        var msg = $"Snd count: {count}.";

        outputChannel.Publish(msg);
        errorMessage = null;
        return true;
    }
}