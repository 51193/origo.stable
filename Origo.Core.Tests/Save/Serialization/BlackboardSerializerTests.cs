using Origo.Core.Save.Serialization;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class BlackboardSerializerTests
{
    private static SndWorld CreateWorld() => TestFactory.CreateSndWorld();

    [Fact]
    public void BlackboardSerializer_RoundTrip_PreservesData()
    {
        var world = CreateWorld();
        var serializer = new BlackboardSerializer(world.JsonCodec, world.ConverterRegistry);
        var bb = new Blackboard.Blackboard();
        bb.Set("intKey", 42);
        bb.Set("strKey", "hello");

        var json = serializer.Serialize(bb);
        var bb2 = new Blackboard.Blackboard();
        serializer.DeserializeInto(bb2, json);

        Assert.Equal(42, bb2.TryGet<int>("intKey").value);
        Assert.Equal("hello", bb2.TryGet<string>("strKey").value);
    }

    [Fact]
    public void BlackboardSerializer_Serialize_EmptyBlackboard_ReturnsValidJson()
    {
        var world = CreateWorld();
        var serializer = new BlackboardSerializer(world.JsonCodec, world.ConverterRegistry);
        var bb = new Blackboard.Blackboard();

        var json = serializer.Serialize(bb);
        Assert.NotNull(json);
        Assert.Contains("{", json);
    }

    [Fact]
    public void BlackboardSerializer_DeserializeInto_OverwritesExisting()
    {
        var world = CreateWorld();
        var serializer = new BlackboardSerializer(world.JsonCodec, world.ConverterRegistry);
        var bb = new Blackboard.Blackboard();
        bb.Set("key1", "old");
        bb.Set("key2", "keep");

        var source = new Blackboard.Blackboard();
        source.Set("key1", "new");
        var json = serializer.Serialize(source);
        serializer.DeserializeInto(bb, json);

        Assert.Equal("new", bb.TryGet<string>("key1").value);
        // DeserializeAll replaces all data
        Assert.False(bb.TryGet<string>("key2").found);
    }
}

// ── Blackboard ─────────────────────────────────────────────────────────
