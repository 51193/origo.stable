using System;
using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Console.CommandImpl;

/// <summary>
///     <c>continue</c> 命令：请求继续上次游戏。
/// </summary>
public sealed class ContinueGameCommandHandler : ConsoleCommandHandlerBase
{
    private readonly SndContext _context;

    public ContinueGameCommandHandler(SndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public override string Name => "continue";
    public override string HelpText => "continue — 请求继续上次游戏。";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var success = _context.RequestContinueGame();
        if (success)
            outputChannel.Publish("Continue game requested.");
        else
            outputChannel.Publish("No continue data available.");

        errorMessage = null;
        return true;
    }
}
