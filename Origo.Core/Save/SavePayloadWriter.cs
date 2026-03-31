using System;
using Origo.Core.Abstractions;

namespace Origo.Core.Save;

internal static class SavePayloadWriter
{
    public static void WriteProgressOnlyToCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string progressJson,
        string progressStateMachinesJson,
        bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));
        ValidateStrictProgressPayload(progressJson, progressStateMachinesJson);

        var currentRel = SavePathLayout.GetCurrentDirectory();
        var currentAbs = fileSystem.CombinePath(saveRootPath, currentRel);
        fileSystem.CreateDirectory(currentAbs);

        var progressRel = SavePathLayout.GetProgressFile(currentRel);
        var progressAbs = fileSystem.CombinePath(saveRootPath, progressRel);
        SavePathResolver.EnsureParentDirectory(fileSystem, progressAbs);
        fileSystem.WriteAllText(progressAbs, progressJson, overwrite);

        var progressSmRel = SavePathLayout.GetProgressStateMachinesFile(currentRel);
        var progressSmAbs = fileSystem.CombinePath(saveRootPath, progressSmRel);
        SavePathResolver.EnsureParentDirectory(fileSystem, progressSmAbs);
        fileSystem.WriteAllText(progressSmAbs, progressStateMachinesJson, overwrite);
    }

    public static void WriteToCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        SaveGamePayload payload)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(payload);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        ValidateStrictProgressPayload(payload.ProgressJson, payload.ProgressStateMachinesJson);

        var currentRel = SavePathLayout.GetCurrentDirectory();
        var currentAbs = fileSystem.CombinePath(saveRootPath, currentRel);
        fileSystem.CreateDirectory(currentAbs);

        var markerRel = SavePathLayout.GetWriteInProgressMarker(currentRel);
        var markerAbs = fileSystem.CombinePath(saveRootPath, markerRel);
        fileSystem.WriteAllText(markerAbs, "", true);

        WriteProgressOnlyToCurrent(
            fileSystem,
            saveRootPath,
            payload.ProgressJson,
            payload.ProgressStateMachinesJson);

        var customMetaRel = SavePathLayout.GetCustomMetaFile(currentRel);
        var customMetaAbs = fileSystem.CombinePath(saveRootPath, customMetaRel);
        if (payload.CustomMeta is not null && payload.CustomMeta.Count > 0)
        {
            var metaText = SaveMetaMapCodec.Serialize(payload.CustomMeta);
            SavePathResolver.EnsureParentDirectory(fileSystem, customMetaAbs);
            fileSystem.WriteAllText(customMetaAbs, metaText, true);
        }
        else if (fileSystem.Exists(customMetaAbs))
        {
            fileSystem.Delete(customMetaAbs);
        }

        if (!payload.Levels.TryGetValue(payload.ActiveLevelId, out var level))
            throw new InvalidOperationException(
                $"Active level '{payload.ActiveLevelId}' not found in SaveGamePayload.");

        WriteLevelPayload(fileSystem, saveRootPath, currentRel, level, true);

        fileSystem.Delete(markerAbs);
    }

    public static void WriteLevelPayloadOnly(
        IFileSystem fileSystem,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload level,
        bool overwrite = true) =>
        WriteLevelPayload(fileSystem, saveRootPath, baseDirectoryRel, level, overwrite);

    private static void WriteLevelPayload(
        IFileSystem fileSystem,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload level,
        bool overwrite)
    {
        var levelDirRel = SavePathLayout.GetLevelDirectory(baseDirectoryRel, level.LevelId);
        var levelDirAbs = fileSystem.CombinePath(saveRootPath, levelDirRel);
        fileSystem.CreateDirectory(levelDirAbs);

        var sndSceneRel = SavePathLayout.GetLevelSndSceneFile(levelDirRel);
        var sessionRel = SavePathLayout.GetLevelSessionFile(levelDirRel);

        var sndSceneAbs = fileSystem.CombinePath(saveRootPath, sndSceneRel);
        var sessionAbs = fileSystem.CombinePath(saveRootPath, sessionRel);

        if (string.IsNullOrWhiteSpace(level.SndSceneJson))
            throw new InvalidOperationException(
                $"Level payload '{level.LevelId}' missing required SndSceneJson (strict mode).");
        if (string.IsNullOrWhiteSpace(level.SessionJson))
            throw new InvalidOperationException(
                $"Level payload '{level.LevelId}' missing required SessionJson (strict mode).");
        fileSystem.WriteAllText(sndSceneAbs, level.SndSceneJson, overwrite);
        fileSystem.WriteAllText(sessionAbs, level.SessionJson, overwrite);

        var sessionSmRel = SavePathLayout.GetLevelSessionStateMachinesFile(levelDirRel);
        var sessionSmAbs = fileSystem.CombinePath(saveRootPath, sessionSmRel);
        SavePathResolver.EnsureParentDirectory(fileSystem, sessionSmAbs);
        if (string.IsNullOrWhiteSpace(level.SessionStateMachinesJson))
            throw new InvalidOperationException(
                $"Level payload '{level.LevelId}' missing required SessionStateMachinesJson (strict mode).");
        fileSystem.WriteAllText(sessionSmAbs, level.SessionStateMachinesJson, overwrite);
    }

    private static void ValidateStrictProgressPayload(string progressJson, string progressStateMachinesJson)
    {
        if (string.IsNullOrWhiteSpace(progressJson))
            throw new InvalidOperationException("Missing required ProgressJson (strict mode).");
        if (string.IsNullOrWhiteSpace(progressStateMachinesJson))
            throw new InvalidOperationException("Missing required ProgressStateMachinesJson (strict mode).");
    }
}
