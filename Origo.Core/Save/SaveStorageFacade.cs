using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;

namespace Origo.Core.Save;

/// <summary>
///     存档 I/O 门面：严格模式（early stage）实现。
///     仅负责编排文件读写与目录布局，具体 payload 解析/序列化委托给 Reader/Writer。
/// </summary>
public static class SaveStorageFacade
{
    public static IReadOnlyList<string> EnumerateSaveIds(IFileSystem fileSystem, string saveRootPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        if (!fileSystem.DirectoryExists(saveRootPath))
            return Array.Empty<string>();

        var result = new List<string>();
        foreach (var dir in fileSystem.EnumerateDirectories(saveRootPath))
        {
            var leaf = SavePathResolver.GetLeafDirectoryName(dir);
            if (!leaf.StartsWith("save_", StringComparison.Ordinal))
                continue;
            var id = leaf.Substring("save_".Length);
            if (id.Length == 0)
                continue;
            result.Add(id);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    public static IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData(IFileSystem fileSystem,
        string saveRootPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));

        var ids = EnumerateSaveIds(fileSystem, saveRootPath);
        var list = new List<SaveMetaDataEntry>(ids.Count);
        foreach (var id in ids)
        {
            var saveRel = SavePathLayout.GetSaveDirectory(id);
            var metaRel = SavePathLayout.GetCustomMetaFile(saveRel);
            var metaAbs = fileSystem.CombinePath(saveRootPath, metaRel);
            var meta = fileSystem.Exists(metaAbs)
                ? SaveMetaMapCodec.Parse(fileSystem.ReadAllText(metaAbs))
                : new Dictionary<string, string>();
            list.Add(new SaveMetaDataEntry { SaveId = id, MetaData = meta });
        }

        return list;
    }

    public static void WriteSavePayloadToCurrent(IFileSystem fileSystem, string saveRootPath, SaveGamePayload payload)
    {
        SavePayloadWriter.WriteToCurrent(fileSystem, saveRootPath, payload);
    }

    public static void WriteLevelPayloadOnly(
        IFileSystem fileSystem,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload levelPayload,
        bool overwrite = true)
    {
        SavePayloadWriter.WriteLevelPayloadOnly(fileSystem, saveRootPath, baseDirectoryRel, levelPayload, overwrite);
    }

    public static SaveGamePayload ReadSavePayloadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId)
    {
        return SavePayloadReader.ReadFromCurrent(fileSystem, saveRootPath, saveId, activeLevelId);
    }

    public static SaveGamePayload ReadSavePayloadFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId)
    {
        return SavePayloadReader.ReadFromSnapshot(fileSystem, saveRootPath, saveId, activeLevelId);
    }

    public static string? ReadProgressJsonFromSnapshot(IFileSystem fileSystem, string saveRootPath, string saveId)
    {
        return SavePayloadReader.ReadProgressJsonFromSnapshot(fileSystem, saveRootPath, saveId);
    }

    public static void SnapshotCurrentToSave(
        IFileSystem fileSystem,
        string saveRootPath,
        string baseSaveId,
        string newSaveId)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (string.IsNullOrWhiteSpace(saveRootPath))
            throw new ArgumentException("Save root path cannot be null or whitespace.", nameof(saveRootPath));
        if (string.IsNullOrWhiteSpace(baseSaveId))
            throw new ArgumentException("Base save id cannot be null or whitespace.", nameof(baseSaveId));
        if (string.IsNullOrWhiteSpace(newSaveId))
            throw new ArgumentException("New save id cannot be null or whitespace.", nameof(newSaveId));

        // Early stage strict mode: snapshot is a full copy of current.
        // baseSaveId is kept for API shape and future evolution; no fallback/merge is performed.
        var currentRel = SavePathLayout.GetCurrentDirectory();
        var currentAbs = fileSystem.CombinePath(saveRootPath, currentRel);
        if (!fileSystem.DirectoryExists(currentAbs))
            throw new InvalidOperationException("Missing required current/ directory.");

        var saveRel = SavePathLayout.GetSaveDirectory(newSaveId);
        var saveAbs = fileSystem.CombinePath(saveRootPath, saveRel);
        fileSystem.CreateDirectory(saveAbs);

        foreach (var srcAbs in fileSystem.EnumerateFiles(currentAbs, "*", true))
        {
            var relFromRoot = SavePathResolver.GetRelativePath(saveRootPath, srcAbs);
            var relFromCurrent = SavePathResolver.GetRelativePath(currentRel, relFromRoot);
            var destRel = $"{saveRel}/{relFromCurrent}";
            var destAbs = fileSystem.CombinePath(saveRootPath, destRel);
            SavePathResolver.EnsureParentDirectory(fileSystem, destAbs);
            fileSystem.Copy(srcAbs, destAbs, true);
        }
    }
}