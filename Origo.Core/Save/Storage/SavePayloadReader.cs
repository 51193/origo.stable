using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Save.Meta;

namespace Origo.Core.Save.Storage;

internal static class SavePayloadReader
{
    public static SaveGamePayload ReadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ILogger? logger = null) =>
        ReadFromCurrent(fileSystem, saveRootPath, saveId, activeLevelId, new DefaultSavePathPolicy(), logger);

    public static SaveGamePayload ReadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ISavePathPolicy pathPolicy,
        ILogger? logger = null)
    {
        ValidateSaveRoot(fileSystem, saveRootPath);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        var baseRel = pathPolicy.GetCurrentDirectory();

        var markerRel = pathPolicy.GetWriteInProgressMarker(baseRel);
        var markerAbs = fileSystem.CombinePath(saveRootPath, markerRel);
        if (fileSystem.Exists(markerAbs))
            (logger ?? NullLogger.Instance).Log(LogLevel.Warning, nameof(SavePayloadReader),
                "Detected .write_in_progress marker in current/; save data may be corrupt from an interrupted write.");

        var progressRel = pathPolicy.GetProgressFile(baseRel);
        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(baseRel);
        var (progressJson, progressStateMachinesJson, customMeta) = ReadProgressAndCustomMeta(
            fileSystem,
            saveRootPath,
            baseRel,
            pathPolicy,
            $"Missing required progress.json in current (path='{progressRel}').",
            $"Missing required progress_state_machines.json in current (path='{progressSmRel}').");

        var level = ReadLevelPayload(fileSystem, saveRootPath, baseRel, activeLevelId, pathPolicy);

        var levels = new Dictionary<string, LevelPayload> { [activeLevelId] = level };
        ReadRemainingLevelPayloads(fileSystem, saveRootPath, baseRel, levels, pathPolicy);

        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = activeLevelId,
            ProgressJson = progressJson,
            ProgressStateMachinesJson = progressStateMachinesJson,
            CustomMeta = customMeta,
            Levels = levels
        };
    }

    public static SaveGamePayload ReadFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId) =>
        ReadFromSnapshot(fileSystem, saveRootPath, saveId, activeLevelId, new DefaultSavePathPolicy());

    public static SaveGamePayload ReadFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ISavePathPolicy pathPolicy)
    {
        ValidateSaveRoot(fileSystem, saveRootPath);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        var baseRel = pathPolicy.GetSaveDirectory(saveId);
        var progressRel = pathPolicy.GetProgressFile(baseRel);
        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(baseRel);
        var (progressJson, progressStateMachinesJson, customMeta) = ReadProgressAndCustomMeta(
            fileSystem,
            saveRootPath,
            baseRel,
            pathPolicy,
            $"Missing required progress.json in save '{saveId}' (path='{progressRel}').",
            $"Missing required progress_state_machines.json in save '{saveId}' (path='{progressSmRel}').");

        var level = ReadLevelPayload(fileSystem, saveRootPath, baseRel, activeLevelId, pathPolicy);

        var levels = new Dictionary<string, LevelPayload> { [activeLevelId] = level };
        ReadRemainingLevelPayloads(fileSystem, saveRootPath, baseRel, levels, pathPolicy);

        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = activeLevelId,
            ProgressJson = progressJson,
            ProgressStateMachinesJson = progressStateMachinesJson,
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

    private static (string ProgressJson, string ProgressStateMachinesJson, IReadOnlyDictionary<string, string>?
        CustomMeta)
        ReadProgressAndCustomMeta(
            IFileSystem fileSystem,
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
        var progressJson = fileSystem.ReadAllText(progressAbs);

        var progressSmRel = pathPolicy.GetProgressStateMachinesFile(baseDirectoryRel);
        var progressSmAbs = fileSystem.CombinePath(saveRootPath, progressSmRel);
        if (!fileSystem.Exists(progressSmAbs))
            throw new InvalidOperationException(missingProgressStateMachinesMessage);
        var progressStateMachinesJson = fileSystem.ReadAllText(progressSmAbs);

        var customMetaRel = pathPolicy.GetCustomMetaFile(baseDirectoryRel);
        var customMetaAbs = fileSystem.CombinePath(saveRootPath, customMetaRel);
        var customMeta = fileSystem.Exists(customMetaAbs)
            ? SaveMetaMapCodec.Parse(fileSystem.ReadAllText(customMetaAbs), NullLogger.Instance)
            : null;

        return (progressJson, progressStateMachinesJson, customMeta);
    }

    public static string? ReadProgressJsonFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId) =>
        ReadProgressJsonFromSnapshot(fileSystem, saveRootPath, saveId, new DefaultSavePathPolicy());

    public static string? ReadProgressJsonFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var saveRel = pathPolicy.GetSaveDirectory(saveId);
        var progressRel = pathPolicy.GetProgressFile(saveRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);

        return fileSystem.Exists(progressAbs) ? fileSystem.ReadAllText(progressAbs) : null;
    }

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string levelId) =>
        TryReadLevelPayloadFromCurrent(fileSystem, saveRootPath, levelId, new DefaultSavePathPolicy());

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(pathPolicy);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var currentRel = pathPolicy.GetCurrentDirectory();
        return TryReadLevelPayload(fileSystem, saveRootPath, currentRel, levelId, pathPolicy);
    }

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        IFileSystem fileSystem,
        string snapshotRootPath,
        string saveId,
        string levelId) =>
        TryReadLevelPayloadFromSnapshot(fileSystem, snapshotRootPath, saveId, levelId, new DefaultSavePathPolicy());

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        IFileSystem fileSystem,
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
        return TryReadLevelPayload(fileSystem, snapshotRootPath, saveRel, levelId, pathPolicy);
    }

    private static LevelPayload? TryReadLevelPayload(
        IFileSystem fileSystem,
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
            SndSceneJson = fileSystem.ReadAllText(sndSceneAbs),
            SessionJson = fileSystem.ReadAllText(sessionAbs),
            SessionStateMachinesJson = fileSystem.ReadAllText(sessionSmAbs)
        };
    }

    private static LevelPayload ReadLevelPayload(
        IFileSystem fileSystem,
        string saveRootPath,
        string baseDirectoryRel,
        string levelId,
        ISavePathPolicy pathPolicy)
    {
        return TryReadLevelPayload(fileSystem, saveRootPath, baseDirectoryRel, levelId, pathPolicy)
               ?? throw new InvalidOperationException($"Missing level '{levelId}'.");
    }

    /// <summary>
    ///     枚举基目录下所有 level_* 子目录，读取尚未包含在 <paramref name="levels" /> 中的关卡 payload。
    ///     用于在读取活跃关卡后补充读取后台会话关卡。
    /// </summary>
    private static void ReadRemainingLevelPayloads(
        IFileSystem fileSystem,
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

            var levelPayload = TryReadLevelPayload(fileSystem, saveRootPath, baseDirectoryRel, levelId, pathPolicy);
            if (levelPayload is not null)
                levels[levelId] = levelPayload;
        }
    }
}
