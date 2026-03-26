using System;
using System.Text.Json;
using Origo.Core.Abstractions;
using Origo.Core.Snd;

namespace Origo.Core.Save;

internal sealed class SndSceneJsonSerializer
{
    private readonly SndWorld _world;

    public SndSceneJsonSerializer(SndWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public string Serialize(ISndSceneAccess sceneAccess)
    {
        ArgumentNullException.ThrowIfNull(sceneAccess);
        var metaList = sceneAccess.ExportMetaList();
        return _world.SerializeMetaList(metaList);
    }

    public void DeserializeInto(ISndSceneAccess sceneAccess, string json, bool clearBeforeLoad)
    {
        ArgumentNullException.ThrowIfNull(sceneAccess);
        ArgumentNullException.ThrowIfNull(json);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("SND scene json must be a JSON array.");

        var metaList = _world.Mappings.ResolveMetaListFromJsonArray(
            doc.RootElement,
            _world.JsonOptions);

        if (clearBeforeLoad)
            sceneAccess.ClearAll();
        sceneAccess.LoadFromMetaList(metaList);
    }
}