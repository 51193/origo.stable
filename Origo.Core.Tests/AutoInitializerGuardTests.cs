using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class AutoInitializerGuardTests
{
    [Fact]
    public void DiscoverAndRegisterStrategies_WithoutAttribute_Throws()
    {
        var logger = new TestLogger();
        var world = new SndWorld(new TypeStringMapping(), logger);

        var ex = Assert.Throws<System.InvalidOperationException>(() =>
            OrigoAutoInitializer.DiscoverAndRegisterStrategies(world, logger));
        Assert.Contains(nameof(MissingAttrStrategy), ex.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void IsStatelessStrategyType_WithInstanceFields_ReturnsFalse_AndReportsFieldNames()
    {
        var ok = OrigoAutoInitializer.IsStatelessStrategyType(
            typeof(StatefulAutoInitStrategy), out var mutableFieldNames);

        Assert.False(ok);
        Assert.Contains("_counter", mutableFieldNames, System.StringComparison.Ordinal);
    }

    [StrategyIndex(IndexConst)]
    private sealed class AnnotatedStrategy : EntityStrategyBase
    {
        public const string IndexConst = "annotated.strategy";
    }

    private sealed class MissingAttrStrategy : EntityStrategyBase;

    [StrategyIndex(StatefulAutoInitStrategy.IndexConst)]
    private abstract class StatefulAutoInitStrategy : EntityStrategyBase
    {
        public const string IndexConst = "auto.init.stateful.local";
        private int _counter;
        public override void Process(Origo.Core.Abstractions.ISndEntity entity, double delta, SndContext ctx) => _counter++;
    }
}
