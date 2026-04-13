using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions;
using Origo.Core.Random;
using Origo.Core.Runtime.Console;
using Origo.Core.Save.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
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

public class BlackboardTests
{
    [Fact]
    public void Blackboard_Set_And_TryGet_Int()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("score", 100);
        var (found, value) = bb.TryGet<int>("score");
        Assert.True(found);
        Assert.Equal(100, value);
    }

    [Fact]
    public void Blackboard_Set_And_TryGet_String()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("name", "player");
        var (found, value) = bb.TryGet<string>("name");
        Assert.True(found);
        Assert.Equal("player", value);
    }

    [Fact]
    public void Blackboard_TryGet_MissingKey_ReturnsFalse()
    {
        var bb = new Blackboard.Blackboard();
        var (found, _) = bb.TryGet<int>("missing");
        Assert.False(found);
    }

    [Fact]
    public void Blackboard_TryGet_WrongType_ReturnsFalse()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("key", 42);
        var (found, _) = bb.TryGet<string>("key");
        Assert.False(found);
    }

    [Fact]
    public void Blackboard_Clear_RemovesAll()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("a", 1);
        bb.Set("b", 2);
        bb.Clear();
        Assert.Empty(bb.GetKeys());
    }

    [Fact]
    public void Blackboard_GetKeys_ReturnsAllKeys()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("a", 1);
        bb.Set("b", "x");
        var keys = bb.GetKeys();
        Assert.Equal(2, keys.Count);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void Blackboard_SerializeAll_And_DeserializeAll_RoundTrip()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("key1", 42);
        bb.Set("key2", "val");

        var data = bb.SerializeAll();
        var bb2 = new Blackboard.Blackboard();
        bb2.DeserializeAll(data);

        Assert.Equal(42, bb2.TryGet<int>("key1").value);
        Assert.Equal("val", bb2.TryGet<string>("key2").value);
    }

    [Fact]
    public void Blackboard_Set_OverwriteExisting()
    {
        var bb = new Blackboard.Blackboard();
        bb.Set("key", 1);
        bb.Set("key", 2);
        Assert.Equal(2, bb.TryGet<int>("key").value);
    }

    [Fact]
    public void Blackboard_Set_ThrowsOnNullKey()
    {
        var bb = new Blackboard.Blackboard();
        Assert.Throws<ArgumentException>(() => bb.Set(null!, 1));
    }

    [Fact]
    public void Blackboard_TryGet_ThrowsOnNullKey()
    {
        var bb = new Blackboard.Blackboard();
        Assert.Throws<ArgumentException>(() => bb.TryGet<int>(null!));
    }

    [Fact]
    public void Blackboard_Set_ThrowsOnWhitespaceKey()
    {
        var bb = new Blackboard.Blackboard();
        Assert.Throws<ArgumentException>(() => bb.Set("  ", 1));
    }
}

// ── DataObserverManager ────────────────────────────────────────────────

public class DataObserverManagerExtendedTests
{
    [Fact]
    public void DataObserverManager_Subscribe_And_Notify()
    {
        var mgr = new DataObserverManager();
        object? receivedOld = null, receivedNew = null;
        mgr.Subscribe("hp", (o, n) =>
        {
            receivedOld = o;
            receivedNew = n;
        });

        mgr.NotifyObservers("hp", 100, 80);
        Assert.Equal(100, receivedOld);
        Assert.Equal(80, receivedNew);
    }

    [Fact]
    public void DataObserverManager_Unsubscribe_StopsNotification()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        Action<object?, object?> callback = (_, _) => callCount++;

        mgr.Subscribe("hp", callback);
        mgr.NotifyObservers("hp", null, null);
        Assert.Equal(1, callCount);

        mgr.Unsubscribe("hp", callback);
        mgr.NotifyObservers("hp", null, null);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void DataObserverManager_Subscribe_WithFilter_SkipsFiltered()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        mgr.Subscribe("hp", (_, _) => callCount++, (o, n) => (int)n! > 50);

        mgr.NotifyObservers("hp", 0, 80);
        Assert.Equal(1, callCount);

        mgr.NotifyObservers("hp", 0, 30);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void DataObserverManager_Clear_RemovesAllSubscriptions()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        mgr.Subscribe("hp", (_, _) => callCount++);
        mgr.Clear();
        mgr.NotifyObservers("hp", null, null);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void DataObserverManager_NotifyObservers_UnknownName_DoesNotThrow()
    {
        var mgr = new DataObserverManager();
        mgr.NotifyObservers("nonexistent", null, null);
    }

    [Fact]
    public void DataObserverManager_Unsubscribe_UnknownName_DoesNotThrow()
    {
        var mgr = new DataObserverManager();
        mgr.Unsubscribe("nonexistent", (_, _) => { });
    }

    [Fact]
    public void DataObserverManager_MultipleSubscribers_AllNotified()
    {
        var mgr = new DataObserverManager();
        var count1 = 0;
        var count2 = 0;
        mgr.Subscribe("hp", (_, _) => count1++);
        mgr.Subscribe("hp", (_, _) => count2++);

        mgr.NotifyObservers("hp", null, null);
        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }
}

// ── SndSceneSerializer ─────────────────────────────────────────────

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

public class TypeStringMappingExtendedTests
{
    [Fact]
    public void TypeStringMapping_GetTypeByName_UnregisteredType_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<InvalidOperationException>(() => mapping.GetTypeByName("nonexistent"));
    }

    [Fact]
    public void TypeStringMapping_GetNameByType_UnregisteredType_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<InvalidOperationException>(() => mapping.GetNameByType(typeof(DateTime)));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_DuplicateSameType_NoThrow()
    {
        var mapping = new TypeStringMapping();
        // Re-registering same type->name pair should not throw
        mapping.RegisterType<int>(BclTypeNames.Int32);
    }

    [Fact]
    public void TypeStringMapping_RegisterType_ConflictingNameToType_Throws()
    {
        var mapping = new TypeStringMapping();
        // "Int32" is already mapped to int; trying to map it to long should throw
        Assert.Throws<InvalidOperationException>(() => mapping.RegisterType<long>(BclTypeNames.Int32));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_ConflictingTypeToName_Throws()
    {
        var mapping = new TypeStringMapping();
        // int is already mapped to "Int32"; trying to map it to "MyInt" should throw
        Assert.Throws<InvalidOperationException>(() => mapping.RegisterType<int>("MyInt"));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_WhitespaceName_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<ArgumentException>(() => mapping.RegisterType<Guid>(""));
        Assert.Throws<ArgumentException>(() => mapping.RegisterType<Guid>("  "));
    }

    [Fact]
    public void TypeStringMapping_BclTypes_AllPreregistered()
    {
        var mapping = new TypeStringMapping();
        Assert.Equal(typeof(int), mapping.GetTypeByName(BclTypeNames.Int32));
        Assert.Equal(typeof(string), mapping.GetTypeByName(BclTypeNames.String));
        Assert.Equal(typeof(bool), mapping.GetTypeByName(BclTypeNames.Boolean));
        Assert.Equal(typeof(float), mapping.GetTypeByName(BclTypeNames.Single));
        Assert.Equal(typeof(double), mapping.GetTypeByName(BclTypeNames.Double));
        Assert.Equal(typeof(long), mapping.GetTypeByName(BclTypeNames.Int64));
        Assert.Equal(typeof(short), mapping.GetTypeByName(BclTypeNames.Int16));
        Assert.Equal(typeof(byte), mapping.GetTypeByName(BclTypeNames.Byte));
        Assert.Equal(typeof(string[]), mapping.GetTypeByName(BclTypeNames.ArrayString));
    }

    [Fact]
    public void TypeStringMapping_RegisterCustomType_RoundTrips()
    {
        var mapping = new TypeStringMapping();
        mapping.RegisterType<Guid>("Guid");
        Assert.Equal(typeof(Guid), mapping.GetTypeByName("Guid"));
        Assert.Equal("Guid", mapping.GetNameByType(typeof(Guid)));
    }
}

// ── RandomNumberGenerator additional tests ─────────────────────────────

public class RandomNumberGeneratorExtendedTests
{
    [Fact]
    public void RandomNumberGenerator_SameSeed_ProducesSameSequence()
    {
        var seq1 = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("test-seed"), 10).ToList();
        var seq2 = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("test-seed"), 10).ToList();

        Assert.Equal(seq1, seq2);
    }

    [Fact]
    public void RandomNumberGenerator_DifferentSeed_ProducesDifferentSequence()
    {
        var (s10, s11) = RandomNumberGenerator.CreateStateFromSeed("seed-a");
        var (val1, _, _) = RandomNumberGenerator.NextUInt64(s10, s11);
        var (s20, s21) = RandomNumberGenerator.CreateStateFromSeed("seed-b");
        var (val2, _, _) = RandomNumberGenerator.NextUInt64(s20, s21);

        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void RandomNumberGenerator_NextInt32_ReturnsValue()
    {
        var (s0, s1) = RandomNumberGenerator.CreateStateFromSeed("seed");
        var (val, _, _) = RandomNumberGenerator.NextInt32(s0, s1);
        // Just verifying it returns without throwing; value is deterministic
        Assert.IsType<int>(val);
    }

    [Fact]
    public void RandomNumberGenerator_NextInt64_ReturnsValue()
    {
        var (s0, s1) = RandomNumberGenerator.CreateStateFromSeed("seed");
        var (val, _, _) = RandomNumberGenerator.NextInt64(s0, s1);
        Assert.IsType<long>(val);
    }

    [Fact]
    public void RandomNumberGenerator_Sequence_IsNotConstant()
    {
        var values = ProduceSequence(RandomNumberGenerator.CreateStateFromSeed("varies"), 100).ToHashSet();
        // With 100 values from a good RNG, we expect at least many unique values
        Assert.True(values.Count > 90);
    }

    [Fact]
    public void RandomNumberGenerator_SameState_SameFirstValue()
    {
        var (s0, s1) = RandomNumberGenerator.CreateStateFromSeed("default");
        var (left, _, _) = RandomNumberGenerator.NextUInt64(s0, s1);
        var (right, _, _) = RandomNumberGenerator.NextUInt64(s0, s1);
        Assert.Equal(left, right);
    }

    private static ulong[] ProduceSequence((ulong s0, ulong s1) state, int count)
    {
        var values = new ulong[count];
        var s0 = state.s0;
        var s1 = state.s1;

        foreach (var i in Enumerable.Range(0, count))
        {
            var (value, nextS0, nextS1) = RandomNumberGenerator.NextUInt64(s0, s1);
            values[i] = value;
            s0 = nextS0;
            s1 = nextS1;
        }

        return values;
    }
}

// ── TypedData ──────────────────────────────────────────────────────────

public class TypedDataTests
{
    [Fact]
    public void TypedData_Constructor_StoresTypeAndValue()
    {
        var td = new TypedData(typeof(int), 42);
        Assert.Equal(typeof(int), td.DataType);
        Assert.Equal(42, td.Data);
    }

    [Fact]
    public void TypedData_NullValue_Allowed()
    {
        var td = new TypedData(typeof(string), null);
        Assert.Equal(typeof(string), td.DataType);
        Assert.Null(td.Data);
    }
}

// ── SndMetaData ────────────────────────────────────────────────────────

public class SndMetaDataTests
{
    [Fact]
    public void SndMetaData_DeepClone_CopiesName()
    {
        var meta = new SndMetaData { Name = "test" };
        var clone = meta.DeepClone();
        Assert.Equal("test", clone.Name);
    }

    [Fact]
    public void SndMetaData_DeepClone_CopiesNodeMetaData()
    {
        var meta = new SndMetaData
        {
            Name = "entity",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string> { ["res"] = "sprite.png" } }
        };
        var clone = meta.DeepClone();
        Assert.NotSame(meta.NodeMetaData, clone.NodeMetaData);
        Assert.Equal("sprite.png", clone.NodeMetaData!.Pairs["res"]);
    }

    [Fact]
    public void SndMetaData_DeepClone_CopiesStrategyMetaData()
    {
        var meta = new SndMetaData
        {
            Name = "entity",
            StrategyMetaData = new StrategyMetaData { Indices = new List<string> { "strat1", "strat2" } }
        };
        var clone = meta.DeepClone();
        Assert.NotSame(meta.StrategyMetaData!.Indices, clone.StrategyMetaData!.Indices);
        Assert.Equal(2, clone.StrategyMetaData.Indices.Count);
    }

    [Fact]
    public void SndMetaData_DeepClone_NullNodeMetaData_RemainsNull()
    {
        var meta = new SndMetaData { Name = "e", NodeMetaData = null };
        var clone = meta.DeepClone();
        Assert.Null(clone.NodeMetaData);
    }

    [Fact]
    public void SndMetaData_DefaultValues()
    {
        var meta = new SndMetaData();
        Assert.Equal(string.Empty, meta.Name);
        Assert.Null(meta.NodeMetaData);
        Assert.Null(meta.StrategyMetaData);
        Assert.NotNull(meta.DataMetaData);
    }
}

// ── OrigoRuntime integration ───────────────────────────────────────────

public class OrigoRuntimeBasicTests
{
    [Fact]
    public void OrigoRuntime_Constructor_CreatesSndWorld()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);

        Assert.NotNull(runtime.SndWorld);
        Assert.Same(logger, runtime.Logger);
    }

    [Fact]
    public void OrigoRuntime_ConsoleInputQueue_NullWithoutInjection()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);

        Assert.Null(runtime.ConsoleInput);
        Assert.Null(runtime.ConsoleOutputChannel);
        Assert.Null(runtime.Console);
    }

    [Fact]
    public void OrigoRuntime_WithConsole_CreatesConsole()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var inputQueue = new ConsoleInputQueue();
        var outputChannel = new ConsoleOutputChannel();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(),
            new Blackboard.Blackboard(), inputQueue, outputChannel);

        Assert.NotNull(runtime.Console);
        Assert.Same(inputQueue, runtime.ConsoleInput);
        Assert.Same(outputChannel, runtime.ConsoleOutputChannel);
    }

    [Fact]
    public void OrigoRuntime_ResetConsoleState_ClearsInputQueue()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var inputQueue = new ConsoleInputQueue();
        inputQueue.Enqueue("test");
        var outputChannel = new ConsoleOutputChannel();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(),
            new Blackboard.Blackboard(), inputQueue, outputChannel);

        runtime.ResetConsoleState();
        Assert.False(inputQueue.TryDequeueCommand(out _));
    }

    [Fact]
    public void OrigoRuntime_FlushEndOfFrameDeferred_ExecutesDeferredActions()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);

        var businessRan = false;
        var systemRan = false;
        runtime.EnqueueBusinessDeferred(() => businessRan = true);
        runtime.EnqueueSystemDeferred(() => systemRan = true);
        runtime.FlushEndOfFrameDeferred();

        Assert.True(businessRan);
        Assert.True(systemRan);
    }
}
