using System;
using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Console.CommandImpl;

/// <summary>
///     <c>list_saves</c> 命令：列出所有存档槽位。
/// </summary>
public sealed class ListSavesCommandHandler : ConsoleCommandHandlerBase
{
    private readonly SndContext _context;

    public ListSavesCommandHandler(SndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public override string Name => "list_saves";
    public override string HelpText => "list_saves — 列出所有存档槽位。";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var saves = _context.ListSaves();
        if (saves.Count == 0)
            outputChannel.Publish("No save slots found.");
        else
            outputChannel.Publish($"Save slots ({saves.Count}): {string.Join(", ", saves)}");

        errorMessage = null;
        return true;
    }
}
