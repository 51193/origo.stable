using System;
using Origo.Core.Abstractions;
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
        SndContext? sndContext)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(saveRootPath);
        ArgumentNullException.ThrowIfNull(runtime);
        Logger = logger;
        FileSystem = fileSystem;
        SaveRootPath = saveRootPath;
        Runtime = runtime;
        SndContext = sndContext;
    }

    public ILogger Logger { get; }

    public IFileSystem FileSystem { get; }

    public string SaveRootPath { get; }

    public OrigoRuntime Runtime { get; }

    /// <summary>
    ///     创建流程/会话时用于字符串栈状态机与策略池回调；由 <see cref="SndContext" /> 注入。
    /// </summary>
    public SndContext? SndContext { get; }
}
