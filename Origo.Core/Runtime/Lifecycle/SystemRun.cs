using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Save;
using Origo.Core.Save.Storage;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     系统级运行时实现，持有系统黑板，负责创建 ProgressRun。
///     加载路径统一为：读取进度索引 -> 读取 payload -> 创建 ProgressRun -> 更新 continue 索引。
///     通过 <see cref="ISaveStorageService" /> 与外围存储系统解耦。
/// </summary>
public sealed class SystemRun : ISystemRun
{
    private readonly RunFactory _factory;

    internal SystemRun(RunFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        SystemBlackboard = _factory.Runtime.SystemBlackboard;
    }

    private ISaveStorageService Storage => _factory.StorageService;

    public IBlackboard SystemBlackboard { get; }

    public IProgressRun? LoadOrContinue(string? saveId)
    {
        var effectiveSaveId = ResolveEffectiveSaveId(saveId);

        if (string.IsNullOrWhiteSpace(effectiveSaveId))
            return null;

        var progressJson = Storage.ReadProgressJsonFromSnapshot(effectiveSaveId);
        if (progressJson is null)
            throw new InvalidOperationException(
                $"Missing required progress.json in save '{effectiveSaveId}'.");

        var progressDict = _factory.Runtime.SndWorld.DeserializeTypedDataMap(progressJson);

        if (!progressDict.TryGetValue(WellKnownKeys.ActiveLevelId, out var levelData)
            || levelData.Data is not string activeLevelId
            || string.IsNullOrWhiteSpace(activeLevelId))
            throw new InvalidOperationException(
                $"Cannot determine ActiveLevelId from progress in save '{effectiveSaveId}'.");

        var payload = Storage.ReadSavePayloadFromSnapshot(effectiveSaveId, activeLevelId);
        Storage.DeleteCurrentDirectory();
        Storage.WriteSavePayloadToCurrent(payload);

        var progressRun = _factory.CreateProgressRun(
            effectiveSaveId,
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
