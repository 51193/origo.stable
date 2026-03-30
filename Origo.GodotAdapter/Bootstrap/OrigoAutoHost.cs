using System;
using Godot;
using System.Diagnostics;
using Origo.Core.Abstractions;
using Origo.Core.Logging;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Save;
using Origo.Core.Serialization;
using Origo.GodotAdapter.FileSystem;
using Origo.GodotAdapter.Logging;
using Origo.GodotAdapter.Serialization;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Bootstrap;

/// <summary>
///     可自行创建运行时或绑定已有 Host 的节点。
/// </summary>
[GlobalClass]
public partial class OrigoAutoHost : Node, IOrigoRuntimeProvider
{
    private const string LogTag = nameof(OrigoAutoHost);

    [Export] public NodePath? HostPath { get; set; }
    [Export] public NodePath? SndManagerPath { get; set; }
    [Export] public string SystemBlackboardSaveRoot { get; set; } = "user://origo_saves";
    public GodotSndManager SndManager { get; private set; } = null!;

    /// <summary>
    ///     控制台命令输入队列；UI 将提交的行通过 <see cref="ConsoleInputQueue.Enqueue" /> 投递。
    /// </summary>
    public ConsoleInputQueue? ConsoleInput { get; private set; }

    /// <summary>
    ///     控制台输出发布通道；UI/策略可按生命周期订阅并消费输出。
    /// </summary>
    public IConsoleOutputChannel? ConsoleOutputChannel { get; private set; }

    public OrigoRuntime Runtime { get; private set; } = null!;

    /// <summary>
    ///     与 <see cref="Runtime" /> 同一次引导创建的 <see cref="GodotFileSystem" />，供子类（如 <see cref="OrigoDefaultEntry" />）复用。
    /// </summary>
    protected IFileSystem SharedFileSystem { get; private set; } = null!;

    public override void _Ready()
    {
        var readyWatch = Stopwatch.StartNew();
        var bootstrapLogger = CreateBootstrapLogger();
        bootstrapLogger.Log(LogLevel.Info, LogTag, new LogMessageBuilder().Build("_Ready begin."));
        try
        {
            Runtime = ResolveOrCreateRuntime();
            readyWatch.Stop();
            Runtime.Logger.Log(LogLevel.Info, LogTag,
                new LogMessageBuilder()
                    .SetElapsedMs(readyWatch.Elapsed.TotalMilliseconds)
                    .AddSuffix("hostPath", HostPath?.ToString())
                    .Build("_Ready completed."));
        }
        catch (System.Exception ex)
        {
            readyWatch.Stop();
            bootstrapLogger.Log(LogLevel.Error, LogTag,
                new LogMessageBuilder().SetElapsedMs(readyWatch.Elapsed.TotalMilliseconds)
                    .Build($"_Ready failed: {ex.Message}"));
            throw;
        }
    }

    private OrigoRuntime ResolveOrCreateRuntime()
    {
        SharedFileSystem = new GodotFileSystem();
        var watch = Stopwatch.StartNew();
        if (HostPath is not null && !HostPath.IsEmpty)
        {
            var hostNode = GetNodeOrNull(HostPath);
            if (hostNode is IOrigoRuntimeProvider { Runtime: not null } provider)
            {
                provider.Runtime.Logger.Log(LogLevel.Info, LogTag,
                    new LogMessageBuilder()
                        .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                        .AddSuffix("hostPath", HostPath.ToString())
                        .Build("Resolved runtime from existing host."));
                return provider.Runtime;
            }

            throw new InvalidOperationException(
                $"HostPath '{HostPath}' did not resolve to a ready IOrigoRuntimeProvider with non-null Runtime.");
        }

        return CreateRuntime();
    }

    private OrigoRuntime CreateRuntime()
    {
        var createWatch = Stopwatch.StartNew();
        var logger = CreateBootstrapLogger();
        logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder()
                .AddSuffix("hostPath", HostPath?.ToString())
                .Build("CreateRuntime begin."));

        var sndManager = ResolveOrCreateSndManager();
        SndManager = sndManager;
        sndManager.SetProcess(true);

        var fileSystem = SharedFileSystem;
        var systemBbPath = fileSystem.CombinePath(SystemBlackboardSaveRoot, "system.json");
        var sharedTypeMapping = new TypeStringMapping();
        GodotJsonConverterRegistry.RegisterTypeMappings(sharedTypeMapping);
        var systemJsonOptions =
            OrigoJson.CreateDefaultOptions(sharedTypeMapping, GodotJsonConverterRegistry.AddConverters);
        var persistentBb = new PersistentBlackboard(fileSystem, systemBbPath, systemJsonOptions,
            new Origo.Core.Blackboard.Blackboard());
        persistentBb.LoadFromDisk();

        var consoleInput = new ConsoleInputQueue();
        var consoleOutputChannel = new ConsoleOutputChannel();

        var runtime = new OrigoRuntime(
            logger,
            sndManager,
            sharedTypeMapping,
            GodotJsonConverterRegistry.AddConverters,
            persistentBb,
            consoleInput,
            consoleOutputChannel
        );

        ConsoleInput = consoleInput;
        ConsoleOutputChannel = consoleOutputChannel;

        var consolePump = new OrigoConsolePump { Runtime = runtime };
        AddChild(consolePump);

        createWatch.Stop();
        logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder()
                .SetElapsedMs(createWatch.Elapsed.TotalMilliseconds)
                .AddSuffix("filePath", systemBbPath)
                .Build("CreateRuntime completed."));
        return runtime;
    }

    private GodotSndManager ResolveOrCreateSndManager()
    {
        if (SndManagerPath is not null && !SndManagerPath.IsEmpty)
        {
            var node = GetNodeOrNull<GodotSndManager>(SndManagerPath);
            if (node is not null) return node;
        }

        var manager = new GodotSndManager();
        AddChild(manager);
        return manager;
    }

    private static GodotLogger CreateBootstrapLogger()
    {
        return new GodotLogger(static (level, tag, message) =>
        {
            switch (level)
            {
                case LogLevel.Warning:
                    GD.PushWarning($"[{tag}] {message}");
                    break;
                case LogLevel.Error:
                    GD.PushError($"[{tag}] {message}");
                    break;
                default:
                    GD.Print($"[{tag}] {message}");
                    break;
            }
        });
    }
}