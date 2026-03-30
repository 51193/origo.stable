using System.Collections.Generic;
using System.Text.Json;
using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class SndEntityAfterLoadTests
{
    private const string AIndex = "test.afterload.a";
    private const string BIndex = "test.afterload.b";

    [Fact]
    public void SndEntity_Load_FromJson_InvokesAfterLoad_ForAllStrategies_InIndexOrder()
    {
        var logger = new TestLogger();
        var runtime = new OrigoRuntime(logger, new TestSndSceneHost(), new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        runtime.SndWorld.RegisterStrategy(() => new AfterLoadProbeAStrategy());
        runtime.SndWorld.RegisterStrategy(() => new AfterLoadProbeBStrategy());

        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var nodeFactory = new TestNodeFactory();

        AfterLoadProbeAStrategy.Events = new List<string>();

        try
        {
            var json = """
                       {
                         "name": "E",
                         "node": { "pairs": {} },
                         "strategy": { "indices": ["test.afterload.a", "test.afterload.b"] },
                         "data": { "pairs": {} }
                       }
                       """;
            var meta = JsonSerializer.Deserialize<SndMetaData>(json, runtime.SndWorld.JsonOptions);
            Assert.NotNull(meta);

            var entity = runtime.SndWorld.CreateEntity(nodeFactory, ctx, logger);
            entity.Load(meta!);

            Assert.Equal(new[] { "afterload:a", "afterload:b" }, AfterLoadProbeAStrategy.Events);
        }
        finally
        {
            AfterLoadProbeAStrategy.Events = null;
        }
    }

    [StrategyIndex(AIndex)]
    private sealed class AfterLoadProbeAStrategy : EntityStrategyBase
    {
        public static List<string>? Events { get; set; }
        public override void AfterLoad(Origo.Core.Abstractions.ISndEntity entity, SndContext ctx)
            => Events?.Add("afterload:a");
    }

    [StrategyIndex(BIndex)]
    private sealed class AfterLoadProbeBStrategy : EntityStrategyBase
    {
        public override void AfterLoad(Origo.Core.Abstractions.ISndEntity entity, SndContext ctx)
        {
            AfterLoadProbeAStrategy.Events?.Add("afterload:b");
        }
    }
}

