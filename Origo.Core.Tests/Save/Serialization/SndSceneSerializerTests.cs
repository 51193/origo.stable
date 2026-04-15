using System;
using Origo.Core.Save.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class SndSceneSerializerTests
{
    private static SndWorld CreateWorld() => TestFactory.CreateSndWorld();

    [Fact]
    public void SndSceneSerializer_Serialize_EmptyScene()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);
        var host = new TestSndSceneHost();

        var json = serializer.Serialize(host);
        Assert.NotNull(json);
        Assert.Contains("[", json);
    }

    [Fact]
    public void SndSceneSerializer_RoundTrip_PreservesMetaList()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);

        var host1 = new TestSndSceneHost();
        host1.Spawn(new SndMetaData { Name = "entity1" });

        var json = serializer.Serialize(host1);

        var host2 = new TestSndSceneHost();
        serializer.DeserializeInto(host2, json, true);

        var metaList = host2.SerializeMetaList();
        Assert.Single(metaList);
        Assert.Equal("entity1", metaList[0].Name);
    }

    [Fact]
    public void SndSceneSerializer_DeserializeInto_ClearsBeforeLoad()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);
        var host = new TestSndSceneHost();
        host.Spawn(new SndMetaData { Name = "existing" });

        serializer.DeserializeInto(host, "[]", true);
        Assert.Equal(1, host.ClearAllCount);
    }

    [Fact]
    public void SndSceneSerializer_DeserializeInto_NoClearWhenFalse()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);
        var host = new TestSndSceneHost();

        serializer.DeserializeInto(host, "[]", false);
        Assert.Equal(0, host.ClearAllCount);
    }

    [Fact]
    public void SndSceneSerializer_DeserializeInto_InvalidJson_Throws()
    {
        var world = CreateWorld();
        var serializer = new SndSceneSerializer(world);
        var host = new TestSndSceneHost();

        Assert.ThrowsAny<Exception>(() => serializer.DeserializeInto(host, "{}", true));
    }

    [Fact]
    public void SndSceneSerializer_Constructor_ThrowsOnNullWorld() =>
        Assert.Throws<ArgumentNullException>(() => new SndSceneSerializer(null!));
}

// ── TypeStringMapping additional tests ─────────────────────────────────
