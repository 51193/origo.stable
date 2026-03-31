using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.Logging;

namespace Origo.Core.Save;

/// <summary>
///     存档 I/O 门面：严格模式（early stage）实现。
///     仅负责编排文件读写与目录布局，具体 payload 解析/序列化委托给 Reader/Writer。
/// </summary>
public static class SaveStorageFacade
{
    /// <summary>Directory name prefix for on-disk save slots (e.g. <c>save_001</c>).</summary>
    public const string SaveDirectoryPrefix = "save_";

    /// <summary>
    ///     枚举存档根目录下所有合法的存档槽 ID，按字典序排序返回。
    /// </summary>
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
            if (!leaf.StartsWith(SaveDirectoryPrefix, StringComparison.Ordinal))
                continue;
            var id = leaf.Substring(SaveDirectoryPrefix.Length);
            if (id.Length == 0)
                continue;
            result.Add(id);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    ///     枚举所有存档槽并读取各自的展示元数据（meta.map）。
    /// </summary>
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
                ? SaveMetaMapCodec.Parse(fileSystem.ReadAllText(metaAbs), NullLogger.Instance)
                : new Dictionary<string, string>();
            list.Add(new SaveMetaDataEntry { SaveId = id, MetaData = meta });
        }

        return list;
    }

    /// <summary>
    ///     将存档 payload 写入 <c>current/</c> 活动目录。
    /// </summary>
    public static void
        WriteSavePayloadToCurrent(IFileSystem fileSystem, string saveRootPath, SaveGamePayload payload) =>
        SavePayloadWriter.WriteToCurrent(fileSystem, saveRootPath, payload);

    /// <summary>
    ///     将存档 payload 写入 <c>current/</c> 后，再将 <c>current/</c> 快照复制到 <c>save_{newSaveId}/</c>。
    ///     该两步操作<strong>非原子</strong>：若第二步失败，<c>current/</c> 可能已更新而快照目录未创建，
    ///     活动存档槽与磁盘状态可能不一致；调用方应依赖日志并视需要人工恢复或重试。
    /// </summary>
    public static void WriteSavePayloadToCurrentThenSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        SaveGamePayload payload,
        string baseSaveId,
        string newSaveId,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var currentRel = SavePathLayout.GetCurrentDirectory();
        var markerRel = SavePathLayout.GetWriteInProgressMarker(currentRel);
        var markerAbs = fileSystem.CombinePath(saveRootPath, markerRel);
        var currentAbs = fileSystem.CombinePath(saveRootPath, currentRel);
        fileSystem.CreateDirectory(currentAbs);
        fileSystem.WriteAllText(markerAbs, "", true);

        WriteSavePayloadToCurrent(fileSystem, saveRootPath, payload);

        // WriteToCurrent deletes the marker; re-establish for the snapshot phase
        fileSystem.WriteAllText(markerAbs, "", true);

        try
        {
            SnapshotCurrentToSave(fileSystem, saveRootPath, baseSaveId, newSaveId);
        }
        catch (InvalidOperationException ex)
        {
            logger.Log(LogLevel.Error, nameof(SaveStorageFacade),
                new LogMessageBuilder()
                    .AddSuffix("saveRootPath", saveRootPath)
                    .AddSuffix("baseSaveId", baseSaveId)
                    .AddSuffix("newSaveId", newSaveId)
                    .Build(
                        $"Snapshot failed after current/ was written; save index and disk may be inconsistent. {ex.Message}"));
            throw;
        }

        fileSystem.Delete(markerAbs);
    }

    /// <summary>
    ///     仅写入单个关卡的 payload 数据到指定基目录下。
    /// </summary>
    public static void WriteLevelPayloadOnly(
        IFileSystem fileSystem,
        string saveRootPath,
        string baseDirectoryRel,
        LevelPayload levelPayload,
        bool overwrite = true) =>
        SavePayloadWriter.WriteLevelPayloadOnly(fileSystem, saveRootPath, baseDirectoryRel, levelPayload, overwrite);

    /// <summary>
    ///     从 <c>current/</c> 活动目录读取完整的存档 payload。
    /// </summary>
    public static SaveGamePayload ReadSavePayloadFromCurrent(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId,
        ILogger? logger = null) =>
        SavePayloadReader.ReadFromCurrent(fileSystem, saveRootPath, saveId, activeLevelId, logger);

    /// <summary>
    ///     从指定存档槽的快照目录读取完整的存档 payload。
    /// </summary>
    public static SaveGamePayload ReadSavePayloadFromSnapshot(
        IFileSystem fileSystem,
        string saveRootPath,
        string saveId,
        string activeLevelId) =>
        SavePayloadReader.ReadFromSnapshot(fileSystem, saveRootPath, saveId, activeLevelId);

    /// <summary>
    ///     从指定存档槽的快照目录中仅读取 Progress 黑板 JSON。
    /// </summary>
    public static string? ReadProgressJsonFromSnapshot(IFileSystem fileSystem, string saveRootPath, string saveId) =>
        SavePayloadReader.ReadProgressJsonFromSnapshot(fileSystem, saveRootPath, saveId);

    /// <summary>
    ///     将 <c>current/</c> 目录完整复制为指定存档槽的快照。
    /// </summary>
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
        var tempRel = $"{saveRel}.tmp";
        var tempAbs = fileSystem.CombinePath(saveRootPath, tempRel);

        // Clean up any leftover temp directory from a previous interrupted snapshot.
        if (fileSystem.DirectoryExists(tempAbs))
            fileSystem.DeleteDirectory(tempAbs);

        fileSystem.CreateDirectory(tempAbs);

        try
        {
            foreach (var srcAbs in fileSystem.EnumerateFiles(currentAbs, "*", true))
            {
                var relFromRoot = SavePathResolver.GetRelativePath(saveRootPath, srcAbs);
                var relFromCurrent = SavePathResolver.GetRelativePath(currentRel, relFromRoot);
                var destRel = $"{tempRel}/{relFromCurrent}";
                var destAbs = fileSystem.CombinePath(saveRootPath, destRel);
                SavePathResolver.EnsureParentDirectory(fileSystem, destAbs);
                fileSystem.Copy(srcAbs, destAbs, true);
            }
        }
        catch (Exception ex)
        {
            // Clean up incomplete temp directory on failure (best-effort; suppressed to avoid masking the original exception).
            try
            {
                fileSystem.DeleteDirectory(tempAbs);
            }
            catch
            {
                /* best-effort cleanup */
            }

            throw new InvalidOperationException(
                $"Snapshot from current/ to save '{newSaveId}' failed during copy phase.", ex);
        }

        // Remove previous save directory if it exists, then atomically rename temp → final.
        if (fileSystem.DirectoryExists(saveAbs))
            fileSystem.DeleteDirectory(saveAbs);

        fileSystem.Rename(tempAbs, saveAbs);
    }
}
