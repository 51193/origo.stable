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
        string activeLevelId)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var currentRel = SavePathLayout.GetCurrentDirectory();

        var progressRel = SavePathLayout.GetProgressFile(currentRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);
        if (!fileSystem.Exists(progressAbs))
            throw new InvalidOperationException($"Missing required progress.json in current (path='{progressRel}').");
        var progressJson = fileSystem.ReadAllText(progressAbs);

        var progressSmRel = SavePathLayout.GetProgressStateMachinesFile(currentRel);
        var progressSmAbs = fileSystem.CombinePath(saveRootPath, progressSmRel);
        if (!fileSystem.Exists(progressSmAbs))
            throw new InvalidOperationException(
                $"Missing required progress_state_machines.json in current (path='{progressSmRel}').");
        var progressStateMachinesJson = fileSystem.ReadAllText(progressSmAbs);

        var customMetaRel = SavePathLayout.GetCustomMetaFile(currentRel);
        var customMetaAbs = fileSystem.CombinePath(saveRootPath, customMetaRel);
        var customMeta = fileSystem.Exists(customMetaAbs)
            ? SaveMetaMapCodec.Parse(fileSystem.ReadAllText(customMetaAbs))
            : null;

        var level = ReadLevelPayload(fileSystem, saveRootPath, currentRel, activeLevelId);

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
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var saveRel = SavePathLayout.GetSaveDirectory(saveId);

        var progressRel = SavePathLayout.GetProgressFile(saveRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);
        if (!fileSystem.Exists(progressAbs))
            throw new InvalidOperationException(
                $"Missing required progress.json in save '{saveId}' (path='{progressRel}').");
        var progressJson = fileSystem.ReadAllText(progressAbs);

        var progressSmRel = SavePathLayout.GetProgressStateMachinesFile(saveRel);
        var progressSmAbs = fileSystem.CombinePath(saveRootPath, progressSmRel);
        if (!fileSystem.Exists(progressSmAbs))
            throw new InvalidOperationException(
                $"Missing required progress_state_machines.json in save '{saveId}' (path='{progressSmRel}').");
        var progressStateMachinesJson = fileSystem.ReadAllText(progressSmAbs);

        var customMetaRel = SavePathLayout.GetCustomMetaFile(saveRel);
        var customMetaAbs = fileSystem.CombinePath(saveRootPath, customMetaRel);
        var customMeta = fileSystem.Exists(customMetaAbs)
            ? SaveMetaMapCodec.Parse(fileSystem.ReadAllText(customMetaAbs))
            : null;

        var level = ReadLevelPayload(fileSystem, saveRootPath, saveRel, activeLevelId);

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

        // Strict mode: partial payload is considered corrupted and must fail-fast.
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
        var levelDirRel = SavePathLayout.GetLevelDirectory(baseDirectoryRel, levelId);
        var sndSceneRel = SavePathLayout.GetLevelSndSceneFile(levelDirRel);
        var sessionRel = SavePathLayout.GetLevelSessionFile(levelDirRel);
        var sessionSmRel = SavePathLayout.GetLevelSessionStateMachinesFile(levelDirRel);

        var sndSceneAbs = fileSystem.CombinePath(saveRootPath, sndSceneRel);
        var sessionAbs = fileSystem.CombinePath(saveRootPath, sessionRel);
        var sessionSmAbs = fileSystem.CombinePath(saveRootPath, sessionSmRel);

        if (!fileSystem.Exists(sndSceneAbs))
            throw new InvalidOperationException(
                $"Missing required snd_scene.json for level '{levelId}' (path='{sndSceneRel}').");
        if (!fileSystem.Exists(sessionAbs))
            throw new InvalidOperationException(
                $"Missing required session.json for level '{levelId}' (path='{sessionRel}').");
        if (!fileSystem.Exists(sessionSmAbs))
            throw new InvalidOperationException(
                $"Missing required session_state_machines.json for level '{levelId}' (path='{sessionSmRel}').");

        var sndSceneJson = fileSystem.ReadAllText(sndSceneAbs);
        var sessionJson = fileSystem.ReadAllText(sessionAbs);
        var sessionSmJson = fileSystem.ReadAllText(sessionSmAbs);

        return new LevelPayload
        {
            LevelId = levelId,
            SndSceneJson = sndSceneJson,
            SessionJson = sessionJson,
            SessionStateMachinesJson = sessionSmJson
        };
    }
}