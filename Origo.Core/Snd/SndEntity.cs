using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions;
using Origo.Core.Logging;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd;

/// <summary>
///     SND 聚合实体。封装数据、节点与策略生命周期，保持 Core 与引擎解耦。
/// </summary>
public sealed class SndEntity : ISndEntity
{
    private const string LogTag = nameof(SndEntity);
    private readonly SndContext _context;
    private readonly SndDataManager _dataManager;
    private readonly ILogger? _logger;
    private readonly INodeHost _nodeHost;
    private readonly SndStrategyManager _strategyManager;

    internal SndEntity(
        INodeFactory nodeFactory,
        SndStrategyPool strategyPool,
        SndMappings mappings,
        SndContext context,
        ILogger? logger = null)
    {
        if (nodeFactory == null) throw new ArgumentNullException(nameof(nodeFactory));
        if (strategyPool == null) throw new ArgumentNullException(nameof(strategyPool));
        if (mappings == null) throw new ArgumentNullException(nameof(mappings));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;

        _dataManager = new SndDataManager(this, logger);
        _nodeHost = new SndNodeManager(nodeFactory, mappings, logger);
        _strategyManager = new SndStrategyManager(strategyPool, logger);
    }

    public string Name { get; private set; } = string.Empty;

    public void SetData<T>(string name, T value)
    {
        _dataManager.SetData(name, value);
    }

    public T GetData<T>(string name)
    {
        return _dataManager.GetData<T>(name);
    }

    public (bool found, T value) TryGetData<T>(string name)
    {
        return _dataManager.TryGetData<T>(name);
    }

    public void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter = null)
    {
        _dataManager.Subscribe(name, callback, filter);
    }

    public void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback)
    {
        _dataManager.Unsubscribe(name, callback);
    }

    public INodeHandle? GetNode(string name)
    {
        return _nodeHost.GetNode(name);
    }

    public IReadOnlyCollection<string> GetNodeNames()
    {
        return _nodeHost.GetNodeNames();
    }

    public void AddStrategy(string index)
    {
        _strategyManager.Add(this, index, _context);
    }

    public void RemoveStrategy(string index)
    {
        _strategyManager.Remove(this, index, _context);
    }

    public void Load(SndMetaData metaData)
    {
        Name = metaData.Name;
        _dataManager.Recover(metaData.DataMetaData ?? new DataMetaData());
        _nodeHost.Recover(metaData.NodeMetaData ?? new NodeMetaData());
        _strategyManager.Load(metaData.StrategyMetaData?.Indices ?? Enumerable.Empty<string>(), this, _context);
        _logger?.Log(LogLevel.Info, LogTag, new LogMessageBuilder().AddSuffix("entityName", Name).Build("Entity loaded."));
    }

    public void Spawn(SndMetaData metaData)
    {
        Name = metaData.Name;
        _dataManager.Recover(metaData.DataMetaData ?? new DataMetaData());
        _nodeHost.Recover(metaData.NodeMetaData ?? new NodeMetaData());
        _strategyManager.Spawn(metaData.StrategyMetaData?.Indices ?? Enumerable.Empty<string>(), this, _context);
        _logger?.Log(LogLevel.Info, LogTag, new LogMessageBuilder().AddSuffix("entityName", Name).Build("Entity spawned."));
    }

    public void Quit()
    {
        _strategyManager.Quit(this, _context);
        _nodeHost.Release();
        _dataManager.Release();
        _logger?.Log(LogLevel.Info, LogTag, new LogMessageBuilder().AddSuffix("entityName", Name).Build("Entity quit."));
    }

    public void Dead()
    {
        _strategyManager.Dead(this, _context);
        _nodeHost.Release();
        _dataManager.Release();
        _logger?.Log(LogLevel.Info, LogTag, new LogMessageBuilder().AddSuffix("entityName", Name).Build("Entity dead."));
    }

    public SndMetaData ExportMetaData()
    {
        var strategyIndices = _strategyManager.ExportIndices(this, _context);

        return new SndMetaData
        {
            Name = Name,
            NodeMetaData = _nodeHost.ExportMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                Indices = new List<string>(strategyIndices)
            },
            DataMetaData = _dataManager.ExportMeta()
        };
    }

    public void Process(double delta)
    {
        _strategyManager.Process(this, delta, _context);
    }
}