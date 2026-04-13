using System;
using System.Diagnostics;
using Godot;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
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
///     自建 Runtime 与 SndManager 的唯一启动入口节点。
/// </summary>
[GlobalClass]
public partial class OrigoAutoHost : Node
{
    private const string LogTag = nameof(OrigoAutoHost);

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

    /// <summary>
    ///     与 <see cref="Runtime" /> 同一次引导创建的 <see cref="GodotFileSystem" />，供子类（如 <see cref="OrigoDefaultEntry" />）复用。
    /// </summary>
    protected IFileSystem SharedFileSystem { get; private set; } = null!;

    public OrigoRuntime Runtime { get; private set; } = null!;

    public override void _Ready()
    {
        var readyWatch = Stopwatch.StartNew();
        var bootstrapLogger = CreateBootstrapLogger();
        bootstrapLogger.Log(LogLevel.Info, LogTag, new LogMessageBuilder().Build("_Ready begin."));
        try
        {
            Runtime = CreateRuntime();
            readyWatch.Stop();
            Runtime.Logger.Log(LogLevel.Info, LogTag,
                new LogMessageBuilder()
                    .SetElapsedMs(readyWatch.Elapsed.TotalMilliseconds)
                    .Build("_Ready completed."));
        }
        catch (Exception ex)
        {
            readyWatch.Stop();
            bootstrapLogger.Log(LogLevel.Error, LogTag,
                new LogMessageBuilder().SetElapsedMs(readyWatch.Elapsed.TotalMilliseconds)
                    .Build($"_Ready failed: {ex.Message}"));
            throw;
        }
    }

    private OrigoRuntime CreateRuntime()
    {
        var createWatch = Stopwatch.StartNew();
        var logger = CreateBootstrapLogger();
        logger.Log(LogLevel.Info, LogTag,
            new LogMessageBuilder().Build("CreateRuntime begin."));

        SharedFileSystem = new GodotFileSystem();
        var sndManager = new GodotSndManager();
        AddChild(sndManager);
        SndManager = sndManager;

        var fileSystem = SharedFileSystem;
        var systemBbPath = fileSystem.CombinePath(SystemBlackboardSaveRoot, "system.json");
        var sharedTypeMapping = new TypeStringMapping();
        GodotJsonConverterRegistry.RegisterTypeMappings(sharedTypeMapping);

        var converterRegistry = DataSourceFactory.CreateDefaultRegistry(sharedTypeMapping);
        GodotJsonConverterRegistry.RegisterDataSourceConverters(converterRegistry);
        var jsonCodec = DataSourceFactory.CreateJsonCodec();
        var mapCodec = DataSourceFactory.CreateMapCodec();

        var persistentBb = new PersistentBlackboard(fileSystem, systemBbPath, jsonCodec, converterRegistry,
            new Blackboard());
        persistentBb.LoadFromDisk();

        var consoleInput = new ConsoleInputQueue();
        var consoleOutputChannel = new ConsoleOutputChannel();

        var runtime = new OrigoRuntime(
            logger,
            sndManager,
            sharedTypeMapping,
            converterRegistry,
            jsonCodec,
            mapCodec,
            persistentBb,
            consoleInput,
            consoleOutputChannel
        );
        sndManager.BindRuntimeDependencies(runtime.SndWorld, runtime.Logger);
        sndManager.SetProcess(true);

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
