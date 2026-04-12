using System;
using Origo.Core.Abstractions;
using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Xunit;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Tests;

public class AutoInitializerGuardTests
{
    [Fact]
    public void DiscoverAndRegisterStrategies_WithoutAttribute_Throws()
    {
        var logger = new TestLogger();
        var world = TestFactory.CreateSndWorld(logger: logger);

        Assert.Throws<InvalidOperationException>(() =>
            OrigoAutoInitializer.DiscoverAndRegisterStrategies(world, logger));
    }

    [Fact]
    public void IsStatelessStrategyType_WithInstanceFields_ReturnsFalse_AndReportsFieldNames()
    {
        var ok = OrigoAutoInitializer.IsStatelessStrategyType(
            typeof(StatefulAutoInitStrategy), out var mutableFieldNames);

        Assert.False(ok);
        Assert.Contains("_counter", mutableFieldNames, StringComparison.Ordinal);
    }

    [StrategyIndex(IndexConst)]
    private sealed class AnnotatedStrategy : EntityStrategyBase
    {
        public const string IndexConst = "annotated.strategy";
    }

    private sealed class MissingAttrStrategy : EntityStrategyBase;

    [StrategyIndex(IndexConst)]
    private abstract class StatefulAutoInitStrategy : EntityStrategyBase
    {
        public const string IndexConst = "auto.init.stateful.local";
        private int _counter;
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => _counter++;
    }
}
