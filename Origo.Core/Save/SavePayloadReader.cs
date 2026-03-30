using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;

namespace Origo.Core.Save;

internal static class SavePayloadReader
{
    public static SaveGamePayload ReadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ILogger? logger = null)
    {
        ValidateSaveRoot(fileSystem, saveRootPath);
        var baseRel = SavePathLayout.GetCurrentDirectory();

        var markerRel = SavePathLayout.GetWriteInProgressMarker(baseRel);
        var markerAbs = fileSystem.CombinePath(saveRootPath, markerRel);
        if (fileSystem.Exists(markerAbs))
        {
            (logger ?? NullLogger.Instance).Log(LogLevel.Warning, nameof(SavePayloadReader),
                "Detected .write_in_progress marker in current/; save data may be corrupt from an interrupted write.");
        }

        var progressRel = SavePathLayout.GetProgressFile(baseRel);
        var progressSmRel = SavePathLayout.GetProgressStateMachinesFile(baseRel);
        var (progressJson, progressStateMachinesJson, customMeta) = ReadProgressAndCustomMeta(
            fileSystem,
            saveRootPath,
            baseRel,
            $"Missing required progress.json in current (path='{progressRel}').",
            $"Missing required progress_state_machines.json in current (path='{progressSmRel}').");

        var level = ReadLevelPayload(fileSystem, saveRootPath, baseRel, activeLevelId);

        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = activeLevelId,
            ProgressJson = progressJson,
            ProgressStateMachinesJson = progressStateMachinesJson,
            CustomMeta = customMeta,
            Levels = new Dictionary<string, LevelPayload> { [activeLevelId] = level }
        };
    }

    public static SaveGamePayload ReadFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId)
    {
        ValidateSaveRoot(fileSystem, saveRootPath);
        var baseRel = SavePathLayout.GetSaveDirectory(saveId);
        var progressRel = SavePathLayout.GetProgressFile(baseRel);
        var progressSmRel = SavePathLayout.GetProgressStateMachinesFile(baseRel);
        var (progressJson, progressStateMachinesJson, customMeta) = ReadProgressAndCustomMeta(
            fileSystem,
            saveRootPath,
            baseRel,
            $"Missing required progress.json in save '{saveId}' (path='{progressRel}').",
            $"Missing required progress_state_machines.json in save '{saveId}' (path='{progressSmRel}').");

        var level = ReadLevelPayload(fileSystem, saveRootPath, baseRel, activeLevelId);

        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = activeLevelId,
            ProgressJson = progressJson,
            ProgressStateMachinesJson = progressStateMachinesJson,
            CustomMeta = customMeta,
            Levels = new Dictionary<string, LevelPayload> { [activeLevelId] = level }
        };
    }

    private static void ValidateSaveRoot(IFileSystem fileSystem, string saveRootPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));
    }

    private static (string ProgressJson, string ProgressStateMachinesJson, IReadOnlyDictionary<string, string>? CustomMeta)
        ReadProgressAndCustomMeta(
            IFileSystem fileSystem,
            string saveRootPath,
            string baseDirectoryRel,
            string missingProgressMessage,
            string missingProgressStateMachinesMessage)
    {
        var progressRel = SavePathLayout.GetProgressFile(baseDirectoryRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);
        if (!fileSystem.Exists(progressAbs))
            throw new InvalidOperationException(missingProgressMessage);
        var progressJson = fileSystem.ReadAllText(progressAbs);

        var progressSmRel = SavePathLayout.GetProgressStateMachinesFile(baseDirectoryRel);
        var progressSmAbs = fileSystem.CombinePath(saveRootPath, progressSmRel);
        if (!fileSystem.Exists(progressSmAbs))
            throw new InvalidOperationException(missingProgressStateMachinesMessage);
        var progressStateMachinesJson = fileSystem.ReadAllText(progressSmAbs);

        var customMetaRel = SavePathLayout.GetCustomMetaFile(baseDirectoryRel);
        var customMetaAbs = fileSystem.CombinePath(saveRootPath, customMetaRel);
        var customMeta = fileSystem.Exists(customMetaAbs)
            ? SaveMetaMapCodec.Parse(fileSystem.ReadAllText(customMetaAbs), NullLogger.Instance)
            : null;

        return (progressJson, progressStateMachinesJson, customMeta);
    }

    public static string? ReadProgressJsonFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var saveRel = SavePathLayout.GetSaveDirectory(saveId);
        var progressRel = SavePathLayout.GetProgressFile(saveRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);

        return fileSystem.Exists(progressAbs) ? fileSystem.ReadAllText(progressAbs) : null;
    }

    public static LevelPayload? TryReadLevelPayloadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string levelId)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var currentRel = SavePathLayout.GetCurrentDirectory();
        return TryReadLevelPayload(fileSystem, saveRootPath, currentRel, levelId);
    }

    public static LevelPayload? TryReadLevelPayloadFromSnapshot(
        IFileSystem fileSystem,
        string snapshotRootPath,
        string saveId,
        string levelId)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(snapshotRootPath))
            throw new ArgumentException("Snapshot root path cannot be null or whitespace.", nameof(snapshotRootPath));

        var saveRel = SavePathLayout.GetSaveDirectory(saveId);
        return TryReadLevelPayload(fileSystem, snapshotRootPath, saveRel, levelId);
    }

    private static LevelPayload? TryReadLevelPayload(
        IFileSystem fileSystem,
        string rootPath,
        string baseDirectoryRel,
        string levelId)
    {
        var levelDirRel = SavePathLayout.GetLevelDirectory(baseDirectoryRel, levelId);
        var sndSceneRel = SavePathLayout.GetLevelSndSceneFile(levelDirRel);
        var sessionRel = SavePathLayout.GetLevelSessionFile(levelDirRel);
        var sessionSmRel = SavePathLayout.GetLevelSessionStateMachinesFile(levelDirRel);

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
        string levelId)
    {
        return TryReadLevelPayload(fileSystem, saveRootPath, baseDirectoryRel, levelId)
               ?? throw new InvalidOperationException($"Missing level '{levelId}'.");
    }
}