using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions;

namespace Origo.Core.Save;

internal static class SaveSnapshotService
{
    public static void SnapshotCurrentToSave(
        IFileSystem fileSystem,
        string saveRootPath,
        string baseSaveId,
        string newSaveId)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var currentRel = SavePathLayout.GetCurrentDirectory();
        var baseRel = SavePathLayout.GetSaveDirectory(baseSaveId);
        var newRel = SavePathLayout.GetSaveDirectory(newSaveId);

        var currentAbs = fileSystem.CombinePath(saveRootPath, currentRel);
        var baseAbs = fileSystem.CombinePath(saveRootPath, baseRel);
        var newAbs = fileSystem.CombinePath(saveRootPath, newRel);

        fileSystem.CreateDirectory(newAbs);

        if (fileSystem.DirectoryExists(currentAbs))
            foreach (var sourceFile in fileSystem.EnumerateFiles(currentAbs, "*", true))
            {
                var relative = SavePathResolver.GetRelativePath(currentAbs, sourceFile);
                var destination = fileSystem.CombinePath(newAbs, relative);
                SavePathResolver.EnsureParentDirectory(fileSystem, destination);
                fileSystem.Copy(sourceFile, destination, true);
            }

        if (fileSystem.DirectoryExists(baseAbs))
            foreach (var sourceFile in fileSystem.EnumerateFiles(baseAbs, "*", true))
            {
                var relative = SavePathResolver.GetRelativePath(baseAbs, sourceFile);
                var destination = fileSystem.CombinePath(newAbs, relative);
                if (fileSystem.Exists(destination))
                    continue;

                SavePathResolver.EnsureParentDirectory(fileSystem, destination);
                fileSystem.Copy(sourceFile, destination, false);
            }
    }

    public static IReadOnlyList<string> EnumerateSaveIds(
        IFileSystem fileSystem,
        string saveRootPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        if (!fileSystem.DirectoryExists(saveRootPath))
            return [];

        const string prefix = "save_";
        return fileSystem.EnumerateDirectories(saveRootPath)
            .Select(SavePathResolver.GetLeafDirectoryName)
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => name.Substring(prefix.Length))
            .ToList();
    }

    public static IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData(
        IFileSystem fileSystem,
        string saveRootPath)
    {
        var saveIds = EnumerateSaveIds(fileSystem, saveRootPath);
        var result = new List<SaveMetaDataEntry>(saveIds.Count);

        foreach (var saveId in saveIds)
        {
            var saveRel = SavePathLayout.GetSaveDirectory(saveId);
            var metaRel = SavePathLayout.GetCustomMetaFile(saveRel);
            var metaAbs = fileSystem.CombinePath(saveRootPath, metaRel);
            var map = fileSystem.Exists(metaAbs)
                ? SaveMetaMapCodec.Parse(fileSystem.ReadAllText(metaAbs))
                : new Dictionary<string, string>();

            result.Add(new SaveMetaDataEntry
            {
                SaveId = saveId,
                MetaData = map
            });
        }

        return result;
    }
}