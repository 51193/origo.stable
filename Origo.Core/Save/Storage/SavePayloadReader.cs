using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;

namespace Origo.Core.Save.Storage;

internal static class SavePayloadReader
{
    public static SaveGamePayload ReadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ILogger? logger = null) =>
        ReadFromCurrent(
            fileSystem,
            DataSourceFactory.CreateDefaultIoGateway(fileSystem, false),
            saveRootPath,
            saveId,
            activeLevelId,
            logger);

    public static SaveGamePayload ReadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ILogger? logger = null) =>
        ReadFromCurrent(fileSystem, dataSourceIo, saveRootPath, saveId, activeLevelId, new DefaultSavePathPolicy(),
            logger);

    public static SaveGamePayload ReadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ISavePathPolicy pathPolicy,
        ILogger? logger = null)
    {
        ValidateSaveRoot(fileSystem, saveRootPath);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        var baseRel = pathPolicy.GetCurrentDirectory();

        var markerRel = pathPolicy.GetWriteInProgressMarker(baseRel);
        var markerAbs = fileSystem.CombinePath(saveRootPath, markerRel);
        if (fileSystem.Exists(markerAbs))
        {
            var ex = new InvalidOperationException(
                $"Detected write-in-progress marker at '{markerRel}' in current/; interrupted save write must be handled before loading.");
            (logger ?? NullLogger.Instance).Log(
                LogLevel.Error,
                nameof(SavePayloadReader),
                ex.Message);
            throw ex;
        }

        var progressRel = pathPolicy.GetProgressFile(baseRel);
        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(baseRel);
        var (progressNode, progressStateMachinesNode, customMeta) = ReadProgressAndCustomMeta(
            fileSystem,
            dataSourceIo,
            saveRootPath,
            baseRel,
            pathPolicy,
            $"Missing required progress.json in current (path='{progressRel}').",
            $"Missing required progress_state_machines.json in current (path='{progressSmRel}').");

        var level = ReadLevelPayload(fileSystem, dataSourceIo, saveRootPath, baseRel, activeLevelId, pathPolicy);

        var levels = new Dictionary<string, LevelPayload> { [activeLevelId] = level };
        ReadRemainingLevelPayloads(fileSystem, dataSourceIo, saveRootPath, baseRel, levels, pathPolicy);

        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = activeLevelId,
            ProgressNode = progressNode,
            ProgressStateMachinesNode = progressStateMachinesNode,
            CustomMeta = customMeta,
            Levels = levels
        };
    }

    public static SaveGamePayload ReadFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId) =>
        ReadFromSnapshot(fileSystem, DataSourceFactory.CreateDefaultIoGateway(fileSystem, false), saveRootPath, saveId,
            activeLevelId);

    public static SaveGamePayload ReadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId) =>
        ReadFromSnapshot(fileSystem, dataSourceIo, saveRootPath, saveId, activeLevelId, new DefaultSavePathPolicy());

    public static SaveGamePayload ReadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ISavePathPolicy pathPolicy)
    {
        ValidateSaveRoot(fileSystem, saveRootPath);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        var baseRel = pathPolicy.GetSaveDirectory(saveId);
        var progressRel = pathPolicy.GetProgressFile(baseRel);
        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(baseRel);
        var (progressNode, progressStateMachinesNode, customMeta) = ReadProgressAndCustomMeta(
            fileSystem,
            dataSourceIo,
            saveRootPath,
            baseRel,
            pathPolicy,
            $"Missing required progress.json in save '{saveId}' (path='{progressRel}').",
            $"Missing required progress_state_machines.json in save '{saveId}' (path='{progressSmRel}').");

        var level = ReadLevelPayload(fileSystem, dataSourceIo, saveRootPath, baseRel, activeLevelId, pathPolicy);

        var levels = new Dictionary<string, LevelPayload> { [activeLevelId] = level };
        ReadRemainingLevelPayloads(fileSystem, dataSourceIo, saveRootPath, baseRel, levels, pathPolicy);

        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = activeLevelId,
            ProgressNode = progressNode,
            ProgressStateMachinesNode = progressStateMachinesNode,
            CustomMeta = customMeta,
            Levels = levels
        };
    }

    private static void ValidateSaveRoot(IFileSystem fileSystem, string saveRootPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));
    }

    private static (DataSourceNode ProgressNode, DataSourceNode ProgressStateMachinesNode,
        IReadOnlyDictionary<string, string>?
        CustomMeta)
        ReadProgressAndCustomMeta(
            IFileSystem fileSystem,
            IDataSourceIoGateway dataSourceIo,
            string saveRootPath,
            string baseDirectoryRel,
            ISavePathPolicy pathPolicy,
            string missingProgressMessage,
            string missingProgressStateMachinesMessage)
    {
        var progressRel = pathPolicy.GetProgressFile(baseDirectoryRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);
        if (!fileSystem.Exists(progressAbs))
            throw new InvalidOperationException(missingProgressMessage);
        var progressNode = dataSourceIo.ReadTree(progressAbs);

        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(baseDirectoryRel);
        var progressSmAbs = fileSystem.CombinePath(saveRootPath, progressSmRel);
        if (!fileSystem.Exists(progressSmAbs))
            throw new InvalidOperationException(missingProgressStateMachinesMessage);
        var progressStateMachinesNode = dataSourceIo.ReadTree(progressSmAbs);

        var customMetaRel = pathPolicy.GetCustomMetaFile(baseDirectoryRel);
        var customMetaAbs = fileSystem.CombinePath(saveRootPath, customMetaRel);
        var customMeta = TryReadStringMap(fileSystem, dataSourceIo, customMetaAbs);

        return (progressNode, progressStateMachinesNode, customMeta);
    }

    private static IReadOnlyDictionary<string, string>? TryReadStringMap(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string mapFileAbs)
    {
        if (!fileSystem.Exists(mapFileAbs))
            return null;

        using var root = dataSourceIo.ReadTree(mapFileAbs);
        if (root.Kind != DataSourceNodeKind.Object)
            throw new InvalidOperationException(
                $"Expected map file '{mapFileAbs}' to decode as object node.");

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in root.Keys)
        {
            var valueNode = root[key];
            if (valueNode.Kind is DataSourceNodeKind.Object or DataSourceNodeKind.Array)
                throw new InvalidOperationException(
                    $"Map file '{mapFileAbs}' key '{key}' must be scalar.");
            result[key] = valueNode.AsString();
        }

        return result;
    }

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId) =>
        ReadProgressNodeFromSnapshot(fileSystem, DataSourceFactory.CreateDefaultIoGateway(fileSystem, false),
            saveRootPath,
            saveId);

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId) =>
        ReadProgressNodeFromSnapshot(fileSystem, dataSourceIo, saveRootPath, saveId, new DefaultSavePathPolicy());

    public static DataSourceNode? ReadProgressNodeFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string saveId,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var saveRel = pathPolicy.GetSaveDirectory(saveId);
        var progressRel = pathPolicy.GetProgressFile(saveRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);

        return fileSystem.Exists(progressAbs) ? dataSourceIo.ReadTree(progressAbs) : null;
    }

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string levelId) =>
        TryReadLevelPayloadFromCurrent(fileSystem, DataSourceFactory.CreateDefaultIoGateway(fileSystem, false),
            saveRootPath,
            levelId);

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string levelId) =>
        TryReadLevelPayloadFromCurrent(fileSystem, dataSourceIo, saveRootPath, levelId, new DefaultSavePathPolicy());

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var currentRel = pathPolicy.GetCurrentDirectory();
        return TryReadLevelPayload(fileSystem, dataSourceIo, saveRootPath, currentRel, levelId, pathPolicy);
    }

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        IFileSystem fileSystem,
        string snapshotRootPath,
        string saveId,
        string levelId) =>
        TryReadLevelPayloadFromSnapshot(fileSystem, DataSourceFactory.CreateDefaultIoGateway(fileSystem, false),
            snapshotRootPath, saveId, levelId);

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string snapshotRootPath,
        string saveId,
        string levelId) =>
        TryReadLevelPayloadFromSnapshot(fileSystem, dataSourceIo, snapshotRootPath, saveId, levelId,
            new DefaultSavePathPolicy());

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string snapshotRootPath,
        string saveId,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        if (string.IsNullOrWhiteSpace(snapshotRootPath))
            throw new ArgumentException("Snapshot root path cannot be null or whitespace.", nameof(snapshotRootPath));

        var saveRel = pathPolicy.GetSaveDirectory(saveId);
        return TryReadLevelPayload(fileSystem, dataSourceIo, snapshotRootPath, saveRel, levelId, pathPolicy);
    }

    private static LevelPayload? TryReadLevelPayload(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string rootPath,
        string baseDirectoryRel,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        var levelDirRel = pathPolicy.GetLevelDirectory(baseDirectoryRel, levelId);
        var sndSceneRel = pathPolicy.GetLevelSndSceneFile(levelDirRel);
        var sessionRel = pathPolicy.GetLevelSessionFile(levelDirRel);
        var sessionSmRel = pathPolicy.GetLevelSessionStateMachinesFile(levelDirRel);

        var sndSceneAbs = fileSystem.CombinePath(rootPath, sndSceneRel);
        var sessionAbs = fileSystem.CombinePath(rootPath, sessionRel);
        var sessionSmAbs = fileSystem.CombinePath(rootPath, sessionSmRel);

        var hasSndScene = fileSystem.Exists(sndSceneAbs);
        var hasSession = fileSystem.Exists(sessionAbs);
        var hasSessionSm = fileSystem.Exists(sessionSmAbs);

        // All three files absent: no saved level yet — return null (see ProgressRun.LevelSwitch).
        // Strict mode: partial payload (some files present, some missing) is corrupted and must fail-fast.
        if (!hasSndScene && !hasSession && !hasSessionSm)
            return null;
        if (!hasSndScene)
            throw new InvalidOperationException(
                $"Missing required snd_scene.json for level '{levelId}' (path='{sndSceneRel}').");
        if (!hasSession)
            throw new InvalidOperationException(
                $"Missing required session.json for level '{levelId}' (path='{sessionRel}').");
        if (!hasSessionSm)
            throw new InvalidOperationException(
                $"Missing required session_state_machines.json for level '{levelId}' (path='{sessionSmRel}').");

        return new LevelPayload
        {
            LevelId = levelId,
            SndSceneNode = dataSourceIo.ReadTree(sndSceneAbs),
            SessionNode = dataSourceIo.ReadTree(sessionAbs),
            SessionStateMachinesNode = dataSourceIo.ReadTree(sessionSmAbs)
        };
    }

    private static LevelPayload ReadLevelPayload(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseDirectoryRel,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        return TryReadLevelPayload(fileSystem, dataSourceIo, saveRootPath, baseDirectoryRel, levelId, pathPolicy)
               ?? throw new InvalidOperationException($"Missing level '{levelId}'.");
    }

    /// <summary>
    ///     枚举基目录下所有 level_* 子目录，读取尚未包含在 <paramref name="levels" /> 中的关卡 payload。
    ///     用于在读取活跃关卡后补充读取后台会话关卡。
    /// </summary>
    private static void ReadRemainingLevelPayloads(
        IFileSystem fileSystem,
        IDataSourceIoGateway dataSourceIo,
        string saveRootPath,
        string baseDirectoryRel,
        Dictionary<string, LevelPayload> levels,
        ISavePathPolicy pathPolicy)
    {
        var baseAbs = fileSystem.CombinePath(saveRootPath, baseDirectoryRel);
        if (!fileSystem.DirectoryExists(baseAbs))
            return;

        foreach (var dirAbs in fileSystem.EnumerateDirectories(baseAbs))
        {
            var leaf = SavePathResolver.GetLeafDirectoryName(dirAbs);
            if (!leaf.StartsWith(SavePathLayout.LevelDirectoryPrefix, StringComparison.Ordinal))
                continue;

            var levelId = leaf.Substring(SavePathLayout.LevelDirectoryPrefix.Length);
            if (string.IsNullOrWhiteSpace(levelId) || levels.ContainsKey(levelId))
                continue;

            var levelPayload = TryReadLevelPayload(fileSystem, dataSourceIo, saveRootPath, baseDirectoryRel, levelId,
                pathPolicy);
            if (levelPayload is not null)
                levels[levelId] = levelPayload;
        }
    }
}
