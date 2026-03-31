using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.DataSource;
using Origo.Core.DataSource.Converters;
using Origo.Core.Snd;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public class DataSourceTests
{
    // ── 1. Factory methods ──

    [Fact]
    public void CreateObject_ReturnsObjectNode()
    {
        var node = DataSourceNode.CreateObject();

        Assert.Equal(DataSourceNodeKind.Object, node.Kind);
    }

    [Fact]
    public void CreateArray_ReturnsArrayNode()
    {
        var node = DataSourceNode.CreateArray();

        Assert.Equal(DataSourceNodeKind.Array, node.Kind);
        Assert.Equal(0, node.Count);
    }

    [Fact]
    public void CreateString_ReturnsStringNode()
    {
        var node = DataSourceNode.CreateString("hello");

        Assert.Equal(DataSourceNodeKind.String, node.Kind);
        Assert.Equal("hello", node.AsString());
    }

    [Fact]
    public void CreateNumber_IntOverloads_ReturnNumberNode()
    {
        var fromInt = DataSourceNode.CreateNumber(42);
        var fromLong = DataSourceNode.CreateNumber(123456789L);
        var fromFloat = DataSourceNode.CreateNumber(3.14f);
        var fromDouble = DataSourceNode.CreateNumber(2.718281828);
        var fromString = DataSourceNode.CreateNumber("99");

        Assert.All(new[] { fromInt, fromLong, fromFloat, fromDouble, fromString },
            n => Assert.Equal(DataSourceNodeKind.Number, n.Kind));
    }

    [Fact]
    public void CreateBoolean_ReturnsBooleanNode()
    {
        var t = DataSourceNode.CreateBoolean(true);
        var f = DataSourceNode.CreateBoolean(false);

        Assert.Equal(DataSourceNodeKind.Boolean, t.Kind);
        Assert.True(t.AsBool());
        Assert.Equal(DataSourceNodeKind.Boolean, f.Kind);
        Assert.False(f.AsBool());
    }

    [Fact]
    public void CreateNull_ReturnsNullNode()
    {
        var node = DataSourceNode.CreateNull();

        Assert.Equal(DataSourceNodeKind.Null, node.Kind);
        Assert.True(node.IsNull);
    }

    // ── 2. Value access ──

    [Fact]
    public void AsString_OnStringNode_ReturnsValue() =>
        Assert.Equal("abc", DataSourceNode.CreateString("abc").AsString());

    [Fact]
    public void AsString_OnNumberNode_ReturnsStringRepresentation() =>
        Assert.Equal("42", DataSourceNode.CreateNumber(42).AsString());

    [Fact]
    public void AsString_OnNullNode_ReturnsEmpty() =>
        Assert.Equal(string.Empty, DataSourceNode.CreateNull().AsString());

    [Fact]
    public void AsInt_ParsesCorrectly() => Assert.Equal(42, DataSourceNode.CreateNumber(42).AsInt());

    [Fact]
    public void AsLong_ParsesCorrectly() =>
        Assert.Equal(9876543210L, DataSourceNode.CreateNumber(9876543210L).AsLong());

    [Fact]
    public void AsFloat_ParsesCorrectly() => Assert.Equal(3.14f, DataSourceNode.CreateNumber(3.14f).AsFloat(), 0.001f);

    [Fact]
    public void AsDouble_ParsesCorrectly() =>
        Assert.Equal(2.718281828, DataSourceNode.CreateNumber(2.718281828).AsDouble(), 0.000001);

    // ── 3. Object access ──

    [Fact]
    public void ObjectNode_IndexerByKey_ReturnsChild()
    {
        var obj = DataSourceNode.CreateObject()
            .Add("x", DataSourceNode.CreateString("val"));

        Assert.Equal("val", obj["x"].AsString());
    }

    [Fact]
    public void ObjectNode_IndexerByKey_ThrowsOnMissingKey()
    {
        var obj = DataSourceNode.CreateObject();

        Assert.Throws<KeyNotFoundException>(() => obj["missing"]);
    }

    [Fact]
    public void ObjectNode_TryGetValue_ReturnsTrueForExistingKey()
    {
        var obj = DataSourceNode.CreateObject()
            .Add("k", DataSourceNode.CreateNumber(1));

        Assert.True(obj.TryGetValue("k", out var child));
        Assert.NotNull(child);
        Assert.Equal(1, child!.AsInt());
    }

    [Fact]
    public void ObjectNode_TryGetValue_ReturnsFalseForMissingKey()
    {
        var obj = DataSourceNode.CreateObject();

        Assert.False(obj.TryGetValue("nope", out _));
    }

    [Fact]
    public void ObjectNode_ContainsKey_WorksCorrectly()
    {
        var obj = DataSourceNode.CreateObject()
            .Add("a", DataSourceNode.CreateNull());

        Assert.True(obj.ContainsKey("a"));
        Assert.False(obj.ContainsKey("b"));
    }

    [Fact]
    public void ObjectNode_Keys_ReturnsInsertionOrder()
    {
        var obj = DataSourceNode.CreateObject()
            .Add("z", DataSourceNode.CreateNull())
            .Add("a", DataSourceNode.CreateNull())
            .Add("m", DataSourceNode.CreateNull());

        Assert.Equal(new[] { "z", "a", "m" }, obj.Keys.ToArray());
    }

    // ── 4. Array access ──

    [Fact]
    public void ArrayNode_IndexerByIndex_ReturnsChild()
    {
        var arr = DataSourceNode.CreateArray()
            .Add(DataSourceNode.CreateString("first"))
            .Add(DataSourceNode.CreateString("second"));

        Assert.Equal("first", arr[0].AsString());
        Assert.Equal("second", arr[1].AsString());
    }

    [Fact]
    public void ArrayNode_Count_ReflectsElements()
    {
        var arr = DataSourceNode.CreateArray()
            .Add(DataSourceNode.CreateNull())
            .Add(DataSourceNode.CreateNull())
            .Add(DataSourceNode.CreateNull());

        Assert.Equal(3, arr.Count);
    }

    [Fact]
    public void ArrayNode_Elements_EnumeratesAll()
    {
        var arr = DataSourceNode.CreateArray()
            .Add(DataSourceNode.CreateNumber(1))
            .Add(DataSourceNode.CreateNumber(2));

        var values = arr.Elements.Select(e => e.AsInt()).ToArray();
        Assert.Equal(new[] { 1, 2 }, values);
    }

    // ── 5. Builder chaining ──

    [Fact]
    public void ObjectNode_Add_ReturnsSameNodeForChaining()
    {
        var obj = DataSourceNode.CreateObject();
        var returned = obj.Add("k", DataSourceNode.CreateNull());

        Assert.Same(obj, returned);
    }

    [Fact]
    public void ArrayNode_Add_ReturnsSameNodeForChaining()
    {
        var arr = DataSourceNode.CreateArray();
        var returned = arr.Add(DataSourceNode.CreateNull());

        Assert.Same(arr, returned);
    }

    // ── 6. Lazy expansion ──

    [Fact]
    public void CreateLazy_DoesNotCallExpanderUntilAccessed()
    {
        var callCount = 0;
        var lazy = DataSourceNode.CreateLazy("{\"v\":1}", _ =>
        {
            callCount++;
            return DataSourceNode.CreateObject()
                .Add("v", DataSourceNode.CreateNumber(1));
        });

        Assert.Equal(0, callCount);

        _ = lazy.Kind;

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void CreateLazy_ExpandsOnlyOnce()
    {
        var callCount = 0;
        var lazy = DataSourceNode.CreateLazy("raw", _ =>
        {
            callCount++;
            return DataSourceNode.CreateString("expanded");
        });

        _ = lazy.AsString();
        _ = lazy.AsString();

        Assert.Equal(1, callCount);
    }

    // ── 7. JSON codec round-trip ──

    [Fact]
    public void JsonCodec_RoundTrip_ComplexTree()
    {
        var codec = TestFactory.CreateJsonCodec();

        var original = DataSourceNode.CreateObject()
            .Add("name", DataSourceNode.CreateString("test"))
            .Add("count", DataSourceNode.CreateNumber(42))
            .Add("active", DataSourceNode.CreateBoolean(true))
            .Add("nothing", DataSourceNode.CreateNull())
            .Add("tags", DataSourceNode.CreateArray()
                .Add(DataSourceNode.CreateString("a"))
                .Add(DataSourceNode.CreateString("b")))
            .Add("nested", DataSourceNode.CreateObject()
                .Add("inner", DataSourceNode.CreateNumber(3.14)));

        var json = codec.Encode(original);
        var decoded = codec.Decode(json);

        Assert.Equal("test", decoded["name"].AsString());
        Assert.Equal(42, decoded["count"].AsInt());
        Assert.True(decoded["active"].AsBool());
        Assert.True(decoded["nothing"].IsNull);
        Assert.Equal(2, decoded["tags"].Count);
        Assert.Equal("a", decoded["tags"][0].AsString());
        Assert.Equal("b", decoded["tags"][1].AsString());
        Assert.Equal(3.14, decoded["nested"]["inner"].AsDouble(), 0.001);
    }

    [Fact]
    public void JsonCodec_RoundTrip_TopLevelArray()
    {
        var codec = TestFactory.CreateJsonCodec();

        var original = DataSourceNode.CreateArray()
            .Add(DataSourceNode.CreateNumber(1))
            .Add(DataSourceNode.CreateNumber(2))
            .Add(DataSourceNode.CreateNumber(3));

        var json = codec.Encode(original);
        var decoded = codec.Decode(json);

        Assert.Equal(DataSourceNodeKind.Array, decoded.Kind);
        Assert.Equal(3, decoded.Count);
        Assert.Equal(2, decoded[1].AsInt());
    }

    // ── 8. JSON codec lazy behavior ──

    [Fact]
    public void JsonCodec_Decode_NestedObjectsAreLazy()
    {
        var codec = TestFactory.CreateJsonCodec();
        var json = """{"outer":{"inner":"value"}}""";

        var root = codec.Decode(json);

        // Accessing top-level key should work
        var outer = root["outer"];
        // Inner should be accessible through lazy expansion
        Assert.Equal("value", outer["inner"].AsString());
    }

    [Fact]
    public void JsonCodec_Decode_PrimitivesAreNotLazy()
    {
        var codec = TestFactory.CreateJsonCodec();
        var json = """{"str":"hello","num":42,"bool":true,"nil":null}""";

        var root = codec.Decode(json);

        Assert.Equal(DataSourceNodeKind.String, root["str"].Kind);
        Assert.Equal(DataSourceNodeKind.Number, root["num"].Kind);
        Assert.Equal(DataSourceNodeKind.Boolean, root["bool"].Kind);
        Assert.Equal(DataSourceNodeKind.Null, root["nil"].Kind);
    }

    // ── 9. Map codec round-trip ──

    [Fact]
    public void MapCodec_RoundTrip_FlatObject()
    {
        var codec = TestFactory.CreateMapCodec();

        var original = DataSourceNode.CreateObject()
            .Add("alpha", DataSourceNode.CreateString("one"))
            .Add("beta", DataSourceNode.CreateString("two"));

        var encoded = codec.Encode(original);
        var decoded = codec.Decode(encoded);

        Assert.Equal("one", decoded["alpha"].AsString());
        Assert.Equal("two", decoded["beta"].AsString());
    }

    // ── 10. Map codec parsing edge cases ──

    [Fact]
    public void MapCodec_Decode_IgnoresCommentsAndEmptyLines()
    {
        var codec = TestFactory.CreateMapCodec();
        var text = "# comment\n\nkey: value\n# another comment\nother: data\n";

        var node = codec.Decode(text);

        Assert.Equal(2, node.Keys.Count());
        Assert.Equal("value", node["key"].AsString());
        Assert.Equal("data", node["other"].AsString());
    }

    [Fact]
    public void MapCodec_Decode_HandlesColonsInValues()
    {
        var codec = TestFactory.CreateMapCodec();
        var text = "url: http://example.com:8080/path\n";

        var node = codec.Decode(text);

        Assert.Equal("http://example.com:8080/path", node["url"].AsString());
    }

    [Fact]
    public void MapCodec_Encode_SkipsNullValues()
    {
        var codec = TestFactory.CreateMapCodec();

        var obj = DataSourceNode.CreateObject()
            .Add("keep", DataSourceNode.CreateString("yes"))
            .Add("drop", DataSourceNode.CreateNull());

        var encoded = codec.Encode(obj);

        Assert.Contains("keep: yes", encoded);
        Assert.DoesNotContain("drop", encoded);
    }

    // ── 11. ConverterRegistry ──

    [Fact]
    public void Registry_RegisterAndGet_RoundTrips()
    {
        var registry = new DataSourceConverterRegistry();
        registry.Register(new Int32DataSourceConverter());

        var converter = registry.Get<int>();
        var node = converter.Write(7);
        var value = converter.Read(node);

        Assert.Equal(7, value);
    }

    [Fact]
    public void Registry_ReadWrite_ByGenericType()
    {
        var registry = TestFactory.CreateRegistry();

        var node = registry.Write("hello");
        var value = registry.Read<string>(node);

        Assert.Equal("hello", value);
    }

    [Fact]
    public void Registry_ReadWrite_ByRuntimeType()
    {
        var registry = TestFactory.CreateRegistry();

#pragma warning disable CA2263 // Intentionally testing runtime-typed overload
        var node = registry.Write(typeof(int), 99);
        var value = registry.Read(typeof(int), node);
#pragma warning restore CA2263

        Assert.Equal(99, value);
    }

    [Fact]
    public void Registry_Get_ThrowsForUnregisteredType()
    {
        var registry = new DataSourceConverterRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Get<DateTime>());
    }

    [Fact]
    public void Registry_RuntimeRead_ThrowsForUnregisteredType()
    {
        var registry = new DataSourceConverterRegistry();

#pragma warning disable CA2263 // Intentionally testing runtime-typed overload
        Assert.Throws<InvalidOperationException>(() => registry.Read(typeof(DateTime), DataSourceNode.CreateNull()));
#pragma warning restore CA2263
    }

    [Fact]
    public void Registry_RuntimeWrite_NullReturnsNullNode()
    {
        var registry = new DataSourceConverterRegistry();

        var node = registry.Write(typeof(int), null);

        Assert.True(node.IsNull);
    }

    // ── 12. Primitive converters ──

    [Fact]
    public void PrimitiveConverters_RoundTrip_AllTypes()
    {
        var registry = TestFactory.CreateRegistry();

        Assert.Equal("text", registry.Read<string>(registry.Write("text")));
        Assert.Equal(42, registry.Read<int>(registry.Write(42)));
        Assert.Equal(9876543210L, registry.Read<long>(registry.Write(9876543210L)));
        Assert.Equal(1.5f, registry.Read<float>(registry.Write(1.5f)));
        Assert.Equal(2.718, registry.Read<double>(registry.Write(2.718)), 0.0001);
        Assert.True(registry.Read<bool>(registry.Write(true)));
        Assert.False(registry.Read<bool>(registry.Write(false)));
    }

    // ── 13. TypedData converter ──

    [Fact]
    public void TypedDataConverter_RoundTrip_IntValue()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new TypedData(typeof(int), 42);

        var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(int), result.DataType);
        Assert.Equal(42, result.Data);
    }

    [Fact]
    public void TypedDataConverter_RoundTrip_StringValue()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new TypedData(typeof(string), "hello");

        var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(string), result.DataType);
        Assert.Equal("hello", result.Data);
    }

    [Fact]
    public void TypedDataConverter_RoundTrip_NullData()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new TypedData(typeof(string), null);

        var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(string), result.DataType);
        Assert.Null(result.Data);
    }

    // ── 14. SndMetaData converter ──

    [Fact]
    public void SndMetaDataConverter_RoundTrip_FullStructure()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new SndMetaData
        {
            Name = "entity1",
            NodeMetaData = new NodeMetaData
            {
                Pairs = new Dictionary<string, string> { ["scene"] = "res://main.tscn" }
            },
            StrategyMetaData = new StrategyMetaData
            {
                Indices = new List<string> { "idle", "walk" }
            },
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData>
                {
                    ["hp"] = new(typeof(int), 100),
                    ["name"] = new(typeof(string), "hero")
                }
            }
        };

        var node = registry.Write(original);
        var result = registry.Read<SndMetaData>(node);

        Assert.Equal("entity1", result.Name);

        Assert.NotNull(result.NodeMetaData);
        Assert.Equal("res://main.tscn", result.NodeMetaData!.Pairs["scene"]);

        Assert.NotNull(result.StrategyMetaData);
        Assert.Equal(new[] { "idle", "walk" }, result.StrategyMetaData!.Indices);

        Assert.NotNull(result.DataMetaData);
        Assert.Equal(typeof(int), result.DataMetaData!.Pairs["hp"].DataType);
        Assert.Equal(100, result.DataMetaData.Pairs["hp"].Data);
        Assert.Equal("hero", result.DataMetaData.Pairs["name"].Data);
    }

    [Fact]
    public void SndMetaDataConverter_RoundTrip_NullSubStructures()
    {
        var registry = TestFactory.CreateRegistry();

        var original = new SndMetaData
        {
            Name = "bare",
            NodeMetaData = null,
            StrategyMetaData = null,
            DataMetaData = null
        };

        var node = registry.Write(original);
        var result = registry.Read<SndMetaData>(node);

        Assert.Equal("bare", result.Name);
        Assert.Null(result.NodeMetaData);
        Assert.Null(result.StrategyMetaData);
        // DataMetaData defaults to new() in SndMetaData, so a null round-trip yields empty
        Assert.NotNull(result.DataMetaData);
        Assert.Empty(result.DataMetaData!.Pairs);
    }

    // ── 15. BlackboardData converter ──

    [Fact]
    public void BlackboardDataConverter_RoundTrip_MixedEntries()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new Dictionary<string, TypedData>
        {
            ["score"] = new(typeof(int), 999),
            ["player"] = new(typeof(string), "Alice"),
            ["alive"] = new(typeof(bool), true)
        } as IReadOnlyDictionary<string, TypedData>;

        var node = registry.Write(original);
        var result = registry.Read<IReadOnlyDictionary<string, TypedData>>(node);

        Assert.Equal(3, result.Count);
        Assert.Equal(999, result["score"].Data);
        Assert.Equal("Alice", result["player"].Data);
        Assert.Equal(true, result["alive"].Data);
    }

    // ── 16. StateMachineContainerPayload converter ──

    [Fact]
    public void StateMachineContainerPayloadConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        var original = new StateMachineContainerPayload
        {
            Machines = new List<StateMachineEntryPayload>
            {
                new()
                {
                    Key = "main",
                    PushIndex = "start",
                    PopIndex = "end",
                    Stack = new List<string> { "stateA", "stateB" }
                },
                new()
                {
                    Key = "sub",
                    PushIndex = "init",
                    PopIndex = "",
                    Stack = new List<string> { "only" }
                }
            }
        };

        var node = registry.Write(original);
        var result = registry.Read<StateMachineContainerPayload>(node);

        Assert.Equal(2, result.Machines.Count);

        Assert.Equal("main", result.Machines[0].Key);
        Assert.Equal("start", result.Machines[0].PushIndex);
        Assert.Equal("end", result.Machines[0].PopIndex);
        Assert.Equal(new[] { "stateA", "stateB" }, result.Machines[0].Stack);

        Assert.Equal("sub", result.Machines[1].Key);
        Assert.Equal(new[] { "only" }, result.Machines[1].Stack);
    }

    // ── 17. StringDictionary converter ──

    [Fact]
    public void StringDictionaryConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        var original = new Dictionary<string, string>
        {
            ["lang"] = "en",
            ["region"] = "US"
        } as IReadOnlyDictionary<string, string>;

        var node = registry.Write(original);
        var result = registry.Read<IReadOnlyDictionary<string, string>>(node);

        Assert.Equal(2, result.Count);
        Assert.Equal("en", result["lang"]);
        Assert.Equal("US", result["region"]);
    }

    // ── 18. DataSourceFactory.CreateDefaultRegistry ──

    [Fact]
    public void CreateDefaultRegistry_RegistersAllExpectedTypes()
    {
        var tm = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(tm);

        // Primitives
        Assert.NotNull(registry.Get<string>());
        Assert.NotNull(registry.Get<int>());
        Assert.NotNull(registry.Get<long>());
        Assert.NotNull(registry.Get<float>());
        Assert.NotNull(registry.Get<double>());
        Assert.NotNull(registry.Get<bool>());

        // Domain types
        Assert.NotNull(registry.Get<TypedData>());
        Assert.NotNull(registry.Get<NodeMetaData>());
        Assert.NotNull(registry.Get<StrategyMetaData>());
        Assert.NotNull(registry.Get<DataMetaData>());
        Assert.NotNull(registry.Get<SndMetaData>());
        Assert.NotNull(registry.Get<IReadOnlyList<SndMetaData>>());
        Assert.NotNull(registry.Get<IReadOnlyDictionary<string, TypedData>>());
        Assert.NotNull(registry.Get<IReadOnlyDictionary<string, string>>());
        Assert.NotNull(registry.Get<StateMachineContainerPayload>());
    }

    // ── Additional edge-case tests ──

    [Fact]
    public void JsonCodec_RoundTrip_EmptyObject()
    {
        var codec = TestFactory.CreateJsonCodec();

        var original = DataSourceNode.CreateObject();
        var json = codec.Encode(original);
        var decoded = codec.Decode(json);

        Assert.Equal(DataSourceNodeKind.Object, decoded.Kind);
        Assert.Empty(decoded.Keys);
    }

    [Fact]
    public void JsonCodec_RoundTrip_EmptyArray()
    {
        var codec = TestFactory.CreateJsonCodec();

        var original = DataSourceNode.CreateArray();
        var json = codec.Encode(original);
        var decoded = codec.Decode(json);

        Assert.Equal(DataSourceNodeKind.Array, decoded.Kind);
        Assert.Equal(0, decoded.Count);
    }

    [Fact]
    public void MapCodec_Encode_ThrowsForNonObjectNode()
    {
        var codec = TestFactory.CreateMapCodec();
        var arr = DataSourceNode.CreateArray();

        Assert.Throws<InvalidOperationException>(() => codec.Encode(arr));
    }

    [Fact]
    public void SndMetaDataConverter_JsonIntegration_FullRoundTrip()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);
        var codec = TestFactory.CreateJsonCodec();

        var original = new SndMetaData
        {
            Name = "npc",
            StrategyMetaData = new StrategyMetaData { Indices = new List<string> { "patrol" } },
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData>
                {
                    ["speed"] = new(typeof(double), 5.5)
                }
            }
        };

        var node = registry.Write(original);
        var json = codec.Encode(node);
        var decodedNode = codec.Decode(json);
        var result = registry.Read<SndMetaData>(decodedNode);

        Assert.Equal("npc", result.Name);
        Assert.Equal(new[] { "patrol" }, result.StrategyMetaData!.Indices);
        Assert.Equal(5.5, (double)result.DataMetaData!.Pairs["speed"].Data!);
    }
}
