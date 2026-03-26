using System.Collections.Generic;
using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class SndEntityAndAutoInitializerTests
{
    private const string LifecycleStrategyIndex = "test.lifecycle";

    [Fact]
    public void SndEntity_SpawnDataNodeStrategyLifecycle_WorksAsExpected()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        var events = new List<string>();
        context.Runtime.SndWorld.RegisterStrategy(() => new LifecycleStrategy(events));

        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger);
        var meta = new SndMetaData
        {
            Name = "Player",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string> { ["root"] = "res://player.tscn" } },
            StrategyMetaData = new StrategyMetaData { Indices = new List<string> { LifecycleStrategyIndex } },
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData> { ["hp"] = new(typeof(int), 10) }
            }
        };

        var callbackCount = 0;
        entity.Subscribe("hp", (_, oldValue, newValue) =>
        {
            callbackCount++;
            Assert.Equal(10, Assert.IsType<int>(oldValue));
            Assert.Equal(20, Assert.IsType<int>(newValue));
        });

        entity.Spawn(meta);
        entity.SetData("hp", 20);
        entity.SetData("hp", 20);
        entity.AddStrategy(LifecycleStrategyIndex);
        entity.RemoveStrategy(LifecycleStrategyIndex);
        _ = entity.ExportMetaData();
        entity.Quit();

        Assert.Equal(1, callbackCount);
        Assert.Contains("AfterSpawn", events);
        Assert.Contains("AfterAdd", events);
        Assert.Contains("BeforeRemove", events);
        Assert.Contains("BeforeSave", events);
        Assert.Contains("BeforeQuit", events);
        Assert.Single(nodeFactory.CreatedHandles);
        Assert.Equal(1, nodeFactory.CreatedHandles[0].FreeCount);
    }

    [Fact]
    public void SndEntity_GetNodeNamesAndGetNode_ReturnExpectedHandles()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger);

        entity.Spawn(new SndMetaData
        {
            Name = "E",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string> { ["root"] = "res://e.tscn" } },
            StrategyMetaData = new StrategyMetaData { Indices = new List<string>() },
            DataMetaData = new DataMetaData()
        });

        Assert.Contains("root", entity.GetNodeNames());
        var handle = entity.GetNode("root");
        Assert.NotNull(handle);
        Assert.Equal("root", handle!.Name);
    }

    [Fact]
    public void SndEntity_AddRemoveStrategy_UpdatesExportedIndices()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        context.Runtime.SndWorld.RegisterStrategy(() => new LifecycleStrategy(new List<string>()));

        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger);
        entity.Spawn(new SndMetaData { Name = "E", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });

        entity.AddStrategy(LifecycleStrategyIndex);
        Assert.Contains(LifecycleStrategyIndex, entity.ExportMetaData().StrategyMetaData!.Indices);

        entity.RemoveStrategy(LifecycleStrategyIndex);
        Assert.DoesNotContain(LifecycleStrategyIndex, entity.ExportMetaData().StrategyMetaData!.Indices);

        // Removing a missing strategy should not throw.
        entity.RemoveStrategy(LifecycleStrategyIndex);
    }

    [Fact]
    public void SndEntity_GetData_MissingKey_ThrowsInvalidOperation()
    {
        var logger = new TestLogger();
        var context = CreateContext(logger);
        var nodeFactory = new TestNodeFactory();
        var entity = context.Runtime.SndWorld.CreateEntity(nodeFactory, context, logger);
        entity.Spawn(new SndMetaData { Name = "E", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });

        Assert.Throws<System.InvalidOperationException>(() => entity.GetData<int>("missing"));
    }

    [Fact]
    public void OrigoAutoInitializer_LoadAndSpawnFromFile_LoadsInlineMetaArray()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("config/entry.json",
            """
            [
              {
                "name": "BootEntity",
                "node": { "pairs": { "root": "res://boot.tscn" } },
                "strategy": { "indices": [] },
                "data": { "pairs": { "ready": { "type": "Boolean", "data": true } } }
              }
            ]
            """);

        var loaded = OrigoAutoInitializer.LoadAndSpawnFromFile("config/entry.json", runtime.Snd, fs, logger);

        Assert.Equal(1, loaded);
        Assert.Single(runtime.Snd.ExportMetaList());
        Assert.Equal("BootEntity", runtime.Snd.ExportMetaList()[0].Name);
    }

    private static SndContext CreateContext(TestLogger logger)
    {
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host);
        var fs = new TestFileSystem();
        return new SndContext(runtime, fs, "user://saveRoot", "res://initial", "res://entry/entry.json");
    }

    [StrategyIndex(LifecycleStrategyIndex)]
    private sealed class LifecycleStrategy : BaseSndStrategy
    {
        private readonly ICollection<string> _events;
        public LifecycleStrategy(ICollection<string> events) => _events = events;
        public override void AfterSpawn(Origo.Core.Abstractions.ISndEntity entity, SndContext ctx) => _events.Add("AfterSpawn");
        public override void AfterAdd(Origo.Core.Abstractions.ISndEntity entity, SndContext ctx) => _events.Add("AfterAdd");
        public override void BeforeRemove(Origo.Core.Abstractions.ISndEntity entity, SndContext ctx) => _events.Add("BeforeRemove");
        public override void BeforeSave(Origo.Core.Abstractions.ISndEntity entity, SndContext ctx) => _events.Add("BeforeSave");
        public override void BeforeQuit(Origo.Core.Abstractions.ISndEntity entity, SndContext ctx) => _events.Add("BeforeQuit");
    }
}

[StrategyIndex(AutoInitStrategyA.IndexConst)]
public sealed class AutoInitStrategyA : BaseSndStrategy
{
    public const string IndexConst = "auto.init.a";
}

[StrategyIndex(AutoInitStrategyB.IndexConst)]
public sealed class AutoInitStrategyB : BaseSndStrategy
{
    public const string IndexConst = "auto.init.b";
}

[StrategyIndex(StatefulAutoInitStrategy.IndexConst)]
public abstract class StatefulAutoInitStrategy : BaseSndStrategy
{
    public const string IndexConst = "auto.init.stateful";
    private int _counter;
    public override void Process(Origo.Core.Abstractions.ISndEntity entity, double delta, SndContext ctx) => _counter++;
}
