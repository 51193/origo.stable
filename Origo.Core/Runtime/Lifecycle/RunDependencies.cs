using System;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Encapsulates the shared dependencies required by the three run types
///     (SystemRun, ProgressRun, SessionRun) so that <see cref="RunFactory" />
///     can act as a thin creation façade.
/// </summary>
internal sealed class RunDependencies
{
    public RunDependencies(
        ILogger logger,
        IFileSystem fileSystem,
        string saveRootPath,
        OrigoRuntime runtime,
        IStateMachineContext? stateMachineContext,
        ISndContext? sndContext,
        ISaveStorageService? storageService = null,
        ISavePathPolicy? savePathPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(saveRootPath);
        ArgumentNullException.ThrowIfNull(runtime);
        Logger = logger;
        FileSystem = fileSystem;
        SaveRootPath = saveRootPath;
        Runtime = runtime;
        StateMachineContext = stateMachineContext;
        SndContext = sndContext;
        SavePathPolicy = savePathPolicy ?? new DefaultSavePathPolicy();
        StorageService = storageService ?? new DefaultSaveStorageService(fileSystem, saveRootPath, SavePathPolicy);
    }

    public ILogger Logger { get; }

    public IFileSystem FileSystem { get; }

    public string SaveRootPath { get; }

    public OrigoRuntime Runtime { get; }

    /// <summary>
    ///     创建流程/会话时用于字符串栈状态机与策略池回调；由 <see cref="Snd.SndContext" /> 注入。
    /// </summary>
    public IStateMachineContext? StateMachineContext { get; }

    /// <summary>
    ///     SND 上下文接口，用于后台会话的场景宿主绑定等操作。
    /// </summary>
    public ISndContext? SndContext { get; }

    /// <summary>
    ///     存档读写服务实例。
    /// </summary>
    public ISaveStorageService StorageService { get; }

    /// <summary>
    ///     存档路径策略。
    /// </summary>
    public ISavePathPolicy SavePathPolicy { get; }
}
