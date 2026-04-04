using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Console.CommandImpl;

/// <summary>
///     <c>load</c> 命令：请求读取存档。
///     用法：<c>load &lt;saveId&gt;</c>
/// </summary>
internal sealed class LoadGameCommandHandler : ConsoleCommandHandlerBase
{
    private readonly SndContext _context;

    public LoadGameCommandHandler(SndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public override string Name => "load";
    public override string HelpText => "load <saveId> — 请求加载指定存档。";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var saveId = invocation.PositionalArgs[0].Trim();
        _context.RequestLoadGame(saveId);
        outputChannel.Publish($"Load requested: '{saveId}'.");
        errorMessage = null;
        return true;
    }
}
