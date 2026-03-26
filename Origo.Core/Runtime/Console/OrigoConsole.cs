using System;
using Origo.Core.Abstractions;
using Origo.Core.Runtime.Console.CommandImpl;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     控制台门面：从输入队列取行、解析、路由到已注册命令；结果通过通道发布。
/// </summary>
public sealed class OrigoConsole
{
    private readonly IConsoleInputSource _input;
    private readonly IConsoleOutputChannel _output;
    private readonly ConsoleCommandRouter _router = new();

    public OrigoConsole(IConsoleInputSource input, IConsoleOutputChannel output, OrigoRuntime runtime)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        ArgumentNullException.ThrowIfNull(runtime);

        _router.Register(new SpawnTemplateCommandHandler(runtime));
        _router.Register(new SndCountCommandHandler(runtime));
    }

    /// <summary>
    ///     处理当前队列中的全部待执行命令（通常每帧或提交时调用一次）。
    /// </summary>
    public void ProcessPending()
    {
        while (_input.TryDequeueCommand(out var line))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!ConsoleCommandParser.TryParse(line!, out var invocation, out var parseError))
            {
                _output.Publish(parseError ?? "Parse error.");
                continue;
            }

            if (invocation == null)
                continue;

            try
            {
                if (!_router.TryExecute(invocation, _output, out var execError) &&
                    !string.IsNullOrEmpty(execError))
                    _output.Publish(execError);
            }
            catch (Exception ex)
            {
                _output.Publish(ex.Message);
            }
        }
    }
}