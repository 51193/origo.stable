using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Console.CommandImpl;

/// <summary>
///     <c>save</c> 命令：请求保存游戏。
///     用法：<c>save &lt;newSaveId&gt; &lt;baseSaveId&gt;</c>
/// </summary>
internal sealed class SaveGameCommandHandler : ConsoleCommandHandlerBase
{
    private readonly SndContext _context;

    public SaveGameCommandHandler(SndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public override string Name => "save";
    public override string HelpText => "save <newSaveId> <baseSaveId> — 请求保存游戏。";
    public override int MinPositionalArgs => 2;
    public override int MaxPositionalArgs => 2;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var newSaveId = invocation.PositionalArgs[0].Trim();
        var baseSaveId = invocation.PositionalArgs[1].Trim();

        _context.RequestSaveGame(newSaveId, baseSaveId);
        outputChannel.Publish($"Save requested: '{newSaveId}' based on '{baseSaveId}'.");
        errorMessage = null;
        return true;
    }
}
