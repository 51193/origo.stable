using System;
using Origo.Core.Abstractions;
using Origo.Core.DataSource;
using Origo.Core.Snd;

namespace Origo.Core.Save;

internal sealed class SndSceneSerializer
{
    private readonly SndWorld _world;

    public SndSceneSerializer(SndWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    public string Serialize(ISndSceneAccess sceneAccess)
    {
        ArgumentNullException.ThrowIfNull(sceneAccess);
        var metaList = sceneAccess.SerializeMetaList();
        return _world.SerializeMetaList(metaList);
    }

    public void DeserializeInto(ISndSceneAccess sceneAccess, string serializedText, bool clearBeforeLoad)
    {
        ArgumentNullException.ThrowIfNull(sceneAccess);
        ArgumentNullException.ThrowIfNull(serializedText);

        var root = _world.JsonCodec.Decode(serializedText);
        if (root.Kind != DataSourceNodeKind.Array)
            throw new InvalidOperationException("SND 场景序列化数据必须为数组格式。");

        var metaList = _world.Mappings.ResolveMetaListFromJsonArray(
            root,
            _world.ConverterRegistry);

        if (clearBeforeLoad)
            sceneAccess.ClearAll();
        sceneAccess.LoadFromMetaList(metaList);
    }
}
