using System;
using System.Collections.Generic;
using System.Text.Json;
using Origo.Core.Abstractions;
using Origo.Core.Save;
using Origo.Core.Snd;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     系统级运行时实现，持有系统黑板，负责创建 ProgressRun。
///     加载路径统一为：读取进度索引 -> 读取 payload -> 创建 ProgressRun -> 更新 continue 索引。
/// </summary>
public sealed class SystemRun : ISystemRun
{
    private readonly RunFactory _factory;

    public SystemRun(RunFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        SystemBlackboard = _factory.Runtime.SystemBlackboard;
    }

    public IBlackboard SystemBlackboard { get; }

    public IProgressRun? LoadOrContinue(string? saveId)
    {
        var effectiveSaveId = ResolveEffectiveSaveId(saveId);

        if (string.IsNullOrWhiteSpace(effectiveSaveId))
            return null;

        var progressJson = SaveStorageFacade.ReadProgressJsonFromSnapshot(
            _factory.FileSystem, _factory.SaveRootPath, effectiveSaveId);
        if (progressJson is null)
            throw new InvalidOperationException(
                $"Missing required progress.json in save '{effectiveSaveId}'.");

        var progressDict = JsonSerializer.Deserialize<Dictionary<string, TypedData>>(
                               progressJson, _factory.Runtime.SndWorld.JsonOptions)
                           ?? throw new InvalidOperationException(
                               $"Failed to deserialize progress.json in save '{effectiveSaveId}'.");

        if (!progressDict.TryGetValue(WellKnownKeys.ActiveLevelId, out var levelData)
            || levelData.Data is not string activeLevelId)
            throw new InvalidOperationException(
                $"Cannot determine ActiveLevelId from progress in save '{effectiveSaveId}'.");

        var payload = SaveStorageFacade.ReadSavePayloadFromSnapshot(
            _factory.FileSystem, _factory.SaveRootPath, effectiveSaveId, activeLevelId);
        SaveStorageFacade.WriteSavePayloadToCurrent(_factory.FileSystem, _factory.SaveRootPath, payload);

        var progressRun = _factory.CreateProgressRun(
            effectiveSaveId,
            activeLevelId,
            new Blackboard.Blackboard());
        progressRun.LoadFromPayload(payload);
        SetActiveSaveSlot(effectiveSaveId);
        return progressRun;
    }

    public void SetActiveSaveSlot(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id cannot be null or whitespace.", nameof(saveId));

        SystemBlackboard.Set(WellKnownKeys.ActiveSaveId, saveId);
    }

    private string ResolveEffectiveSaveId(string? providedSaveId)
    {
        if (!string.IsNullOrWhiteSpace(providedSaveId))
            return providedSaveId;

        var (found, activeSaveId) = SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        return found && !string.IsNullOrWhiteSpace(activeSaveId) ? activeSaveId : string.Empty;
    }
}