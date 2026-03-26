using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;

namespace Origo.Core.Snd;

/// <summary>
///     面向上层的 SND 运行时门面。
///     将 SndWorld（策略池与 JSON 配置）与具体场景宿主 ISndSceneHost 组合在一起，
///     提供统一的 Spawn / 导出入口。
/// </summary>
public sealed class SndRuntime
{
    public SndRuntime(SndWorld world, ISndSceneHost sceneHost)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        SceneHost = sceneHost ?? throw new ArgumentNullException(nameof(sceneHost));
    }

    public SndWorld World { get; }

    public ISndSceneHost SceneHost { get; }

    public ISndEntity Spawn(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        if (string.IsNullOrWhiteSpace(metaData.Name))
            throw new ArgumentException("SndMetaData.Name cannot be null or whitespace.", nameof(metaData));
        if (SceneHost.FindByName(metaData.Name) != null)
            throw new InvalidOperationException($"Snd entity name '{metaData.Name}' already exists.");

        return SceneHost.Spawn(metaData);
    }

    public void SpawnMany(IEnumerable<SndMetaData> metaList)
    {
        if (metaList == null) return;
        foreach (var meta in metaList) Spawn(meta);
    }

    public IReadOnlyList<SndMetaData> ExportMetaList()
    {
        return SceneHost.ExportMetaList();
    }

    public void ClearAll()
    {
        SceneHost.ClearAll();
    }

    public IReadOnlyCollection<ISndEntity> GetEntities()
    {
        return SceneHost.GetEntities();
    }

    public ISndEntity? FindByName(string name)
    {
        return SceneHost.FindByName(name);
    }
}