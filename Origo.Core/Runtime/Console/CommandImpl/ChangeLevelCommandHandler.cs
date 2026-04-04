using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Console.CommandImpl;

/// <summary>
///     <c>change_level</c> 命令：请求切换关卡。
///     用法：<c>change_level &lt;newLevelId&gt;</c>
/// </summary>
internal sealed class ChangeLevelCommandHandler : ConsoleCommandHandlerBase
{
    private readonly SndContext _context;

    public ChangeLevelCommandHandler(SndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public override string Name => "change_level";
    public override string HelpText => "change_level <newLevelId> — 请求切换前台关卡。";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var newLevelId = invocation.PositionalArgs[0].Trim();
        _context.RequestSwitchForegroundLevel(newLevelId);
        outputChannel.Publish($"Level change requested: '{newLevelId}'.");
        errorMessage = null;
        return true;
    }
}
