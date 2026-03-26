using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.GodotAdapter.Snd;

/// <summary>
///     管理场景内 GodotSndEntity 集合的适配层管理器。
///     同时实现 ISndSceneHost，供 Core 层的 SndRuntime 以抽象方式进行实体操作。
///     使用内部 List 维护实体集合，避免每帧通过 Group 查询造成 GC 压力。
/// </summary>
[GlobalClass]
public partial class GodotSndManager : Node, ISndSceneHost
{
    private readonly List<GodotSndEntity> _entities = new();
    private bool _contextBound;

    private bool _runtimeDepsBound;

    public SndWorld SharedWorld { get; private set; } = null!;
    public ILogger SharedLogger { get; private set; } = null!;
    public SndContext? Context { get; private set; }
    public int ProcessTickCount { get; private set; }
    public double ProcessDeltaSum { get; private set; }

    public IReadOnlyList<SndMetaData> ExportMetaList()
    {
        return _entities.Select(s => s.ExportMetaData()).ToList();
    }

    public void LoadFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        foreach (var meta in metaList)
        {
            var snd = CreateSndEntity();
            AddChild(snd);
            _entities.Add(snd);
            snd.Load(meta);
        }

    }

    public void ClearAll()
    {
        QuitAll();
    }

    public ISndEntity Spawn(SndMetaData metaData)
    {
        return SpawnFromMeta(metaData);
    }

    public IReadOnlyCollection<ISndEntity> GetEntities()
    {
        return _entities.Cast<ISndEntity>().ToArray();
    }

    public ISndEntity? FindByName(string name)
    {
        var entity = _entities.FirstOrDefault(s => s.StableName == name);
        return entity;
    }

    public GodotSndEntity SpawnFromMeta(SndMetaData metaData)
    {
        var snd = CreateSndEntity();
        AddChild(snd);
        _entities.Add(snd);
        snd.Spawn(metaData);
        return snd;
    }

    /// <summary>
    ///     绑定与 Core 运行时共享的 SndWorld 与日志（由 <see cref="Bootstrap.OrigoAutoHost" /> 调用一次）。
    /// </summary>
    public void BindRuntimeDependencies(SndWorld world, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(logger);

        if (_runtimeDepsBound)
        {
            throw new InvalidOperationException("Runtime dependencies are already bound.");
        }

        SharedWorld = world;
        SharedLogger = logger;
        _runtimeDepsBound = true;
    }

    /// <summary>
    ///     绑定存档/生命周期门面（由入口节点在创建 <see cref="SndContext" /> 后调用一次）。
    /// </summary>
    public void BindContext(SndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_runtimeDepsBound)
        {
            throw new InvalidOperationException("Call BindRuntimeDependencies before BindContext.");
        }

        if (_contextBound)
        {
            throw new InvalidOperationException("Context is already bound.");
        }

        Context = context;
        _contextBound = true;
    }

    public void QuitAll()
    {
        // 严格栈语义：如果栈 push 顺序与实体 load/spawn 顺序一致，则退出顺序应当反向以匹配 LIFO。
        foreach (var snd in _entities.ToList().AsEnumerable().Reverse())
        {
            _entities.Remove(snd);
            snd.QuitFromManager();
        }

    }

    public void DeadByName(string name)
    {
        var snd = _entities.FirstOrDefault(s => s.StableName == name);
        if (snd == null) return;

        _entities.Remove(snd);
        snd.DeadFromManager();
    }

    public override void _Ready()
    {
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        ProcessTickCount++;
        ProcessDeltaSum += delta;
        foreach (var snd in _entities.ToArray())
            snd.ProcessSnd(delta);
        Context?.FlushDeferredActionsForCurrentFrame();
    }

    private GodotSndEntity CreateSndEntity()
    {
        EnsureReadyForSpawn();
        return new GodotSndEntity(SharedWorld, Context!, SharedLogger);
    }

    private void EnsureReadyForSpawn()
    {
        if (!_runtimeDepsBound || !_contextBound || Context == null)
        {
            throw new InvalidOperationException(
                "GodotSndManager is not ready: call BindRuntimeDependencies and BindContext before spawning entities.");
        }
    }
}