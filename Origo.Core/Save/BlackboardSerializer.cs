using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.DataSource;
using Origo.Core.Snd;

namespace Origo.Core.Save;

internal sealed class BlackboardSerializer
{
    private readonly IDataSourceCodec _codec;
    private readonly DataSourceConverterRegistry _registry;

    public BlackboardSerializer(IDataSourceCodec codec, DataSourceConverterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(registry);
        _codec = codec;
        _registry = registry;
    }

    public string Serialize(IBlackboard blackboard)
    {
        var data = blackboard.SerializeAll();
        var node = _registry.Write<IReadOnlyDictionary<string, TypedData>>(data);
        return _codec.Encode(node);
    }

    public void DeserializeInto(IBlackboard blackboard, string serializedText)
    {
        ArgumentNullException.ThrowIfNull(serializedText);
        var node = _codec.Decode(serializedText);
        var dict = _registry.Read<IReadOnlyDictionary<string, TypedData>>(node);
        if (dict is null)
            throw new InvalidOperationException("Failed to deserialize blackboard data.");
        blackboard.DeserializeAll(dict);
    }
}
