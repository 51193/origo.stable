using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.Logging;

namespace Origo.Core.Snd;

/// <summary>
///     与引擎无关的节点元数据恢复器，具体节点创建由 INodeFactory 提供。
/// </summary>
internal sealed class SndNodeManager : INodeHost
{
    private readonly INodeFactory _factory;
    private readonly ILogger? _logger;
    private readonly SndMappings _mappings;
    private readonly Dictionary<string, INodeHandle> _nodes = new();
    private Dictionary<string, string> _resources = new();

    public SndNodeManager(INodeFactory factory, SndMappings mappings, ILogger? logger = null)
    {
        _factory = factory;
        _mappings = mappings;
        _logger = logger;
    }

    public INodeHandle? GetNode(string name)
    {
        if (_nodes.TryGetValue(name, out var node)) return node;

        _logger?.Log(LogLevel.Error, nameof(SndNodeManager),
            new LogMessageBuilder().AddSuffix("entityName", name).Build("Node not found."));
        return null;
    }

    public IReadOnlyCollection<string> GetNodeNames()
    {
        return _nodes.Keys;
    }

    public void Recover(NodeMetaData metaData)
    {
        Release();
        _resources = new Dictionary<string, string>(metaData.Pairs);

        foreach (var pair in _resources)
        {
            var resourceId = _mappings.ResolveSceneAlias(pair.Value);
            var node = _factory.Create(pair.Key, resourceId);
            if (node == null)
            {
                _logger?.Log(LogLevel.Error, nameof(SndNodeManager),
                    new LogMessageBuilder().AddSuffix("entityName", pair.Key).Build("Failed to create node."));
                continue;
            }

            _nodes[pair.Key] = node;
        }

        _logger?.Log(LogLevel.Info, nameof(SndNodeManager),
            new LogMessageBuilder().Build($"Loaded {_nodes.Count} nodes."));
    }

    public void Release()
    {
        foreach (var node in _nodes.Values) node.Free();

        _nodes.Clear();
        _resources.Clear();
    }

    public NodeMetaData ExportMetaData()
    {
        return new NodeMetaData
        {
            Pairs = new Dictionary<string, string>(_resources)
        };
    }
}