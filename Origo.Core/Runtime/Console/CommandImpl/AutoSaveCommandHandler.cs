using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Console.CommandImpl;

/// <summary>
///     <c>auto_save</c> 命令：请求自动存档。
///     用法：<c>auto_save</c> 或 <c>auto_save &lt;saveId&gt;</c>
/// </summary>
internal sealed class AutoSaveCommandHandler : ConsoleCommandHandlerBase
{
    private readonly SndContext _context;

    public AutoSaveCommandHandler(SndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public override string Name => "auto_save";
    public override string HelpText => "auto_save [saveId] — 请求自动存档，可选指定存档 ID。";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var requestedId = invocation.PositionalArgs.Count > 0
            ? invocation.PositionalArgs[0].Trim()
            : null;

        var resultId = _context.RequestSaveGameAuto(requestedId);
        outputChannel.Publish($"Auto-save completed: '{resultId}'.");
        errorMessage = null;
        return true;
    }
}
