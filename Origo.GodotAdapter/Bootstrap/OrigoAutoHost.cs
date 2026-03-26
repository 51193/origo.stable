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
        var watch = Stopwatch.StartNew();
        if (HostPath != null && !HostPath.IsEmpty)
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

            CreateBootstrapLogger().Log(LogLevel.Warning, LogTag,
                new LogMessageBuilder().AddSuffix("hostPath", HostPath.ToString())
                    .Build("HostPath did not resolve to ready runtime provider, fallback to self-hosting."));
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

        var fileSystem = new GodotFileSystem();
        var systemBbPath = fileSystem.CombinePath(SystemBlackboardSaveRoot, "system.json");
        var systemTypeMapping = new TypeStringMapping();
        GodotJsonConverterRegistry.RegisterTypeMappings(systemTypeMapping);
        var systemJsonOptions =
            OrigoJson.CreateDefaultOptions(systemTypeMapping, GodotJsonConverterRegistry.AddConverters);
        var persistentBb = new PersistentBlackboard(fileSystem, systemBbPath, systemJsonOptions);
        persistentBb.LoadFromDisk();

        var consoleInput = new ConsoleInputQueue();
        var consoleOutputChannel = new ConsoleOutputChannel();

        var runtime = new OrigoRuntime(
            logger,
            sndManager,
            GodotJsonConverterRegistry.AddConverters,
            persistentBb,
            consoleInput,
            consoleOutputChannel
        );

        ConsoleInput = consoleInput;
        ConsoleOutputChannel = consoleOutputChannel;

        runtime.SndWorld.RegisterTypeMappings(GodotJsonConverterRegistry.RegisterTypeMappings);

        sndManager.BindRuntimeDependencies(runtime.SndWorld, logger);

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
        if (SndManagerPath != null && !SndManagerPath.IsEmpty)
        {
            var node = GetNodeOrNull<GodotSndManager>(SndManagerPath);
            if (node != null) return node;
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