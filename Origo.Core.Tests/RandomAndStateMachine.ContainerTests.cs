using System;
using System.Collections.Generic;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public partial class RandomAndStateMachineTests
{
    [Fact]
    public void StateMachineContainer_PopAllRuntime_InvokesBeforeRemoveTopToBottom()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var events = new List<string>();
        SmPushStrategy.PushEvents = events;
        SmPopStrategy.PopRemoveEvents = events;
        SmPopStrategy.PopQuitEvents = events;

        try
        {
            var container = new StateMachineContainer(pool, ctx);
            var sm = container.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
            sm.Push("a");
            sm.Push("b");

            container.PopAllRuntime();
            container.Clear();

            Assert.Equal(
                new[]
                {
                    "push:runtime:null->a",
                    "push:runtime:a->b",
                    "pop:runtime:b->a",
                    "pop:runtime:a->null"
                },
                events);
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_PopAllOnQuit_InvokesBeforeQuitTopToBottom()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var events = new List<string>();
        SmPushStrategy.PushEvents = events;
        SmPopStrategy.PopRemoveEvents = events;
        SmPopStrategy.PopQuitEvents = events;

        try
        {
            var container = new StateMachineContainer(pool, ctx);
            var sm = container.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
            sm.Push("a");
            sm.Push("b");

            container.PopAllOnQuit();
            container.Clear();

            Assert.Equal(
                new[]
                {
                    "push:runtime:null->a",
                    "push:runtime:a->b",
                    "pop:beforeQuit:b->a",
                    "pop:beforeQuit:a->null"
                },
                events);
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_PopAllOnQuit_TraversesMachinesInInsertionOrder()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopOrderProbeStrategy());

        var events = new List<string>();
        SmPopOrderProbeStrategy.Events = events;

        try
        {
            var container = new StateMachineContainer(pool, ctx);
            // insertion order: b -> a
            var smB = container.CreateOrGet("b", "sm.push.test", "sm.pop.orderprobe");
            var smA = container.CreateOrGet("a", "sm.push.test", "sm.pop.orderprobe");

            smB.Push("b1");
            smA.Push("a1");

            container.PopAllOnQuit();
            container.Clear();

            Assert.Equal(new[] { "b", "a" }, events);
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_DeserializeWithoutHooks_ThrowsOnEmptyJson()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var pool = runtime.SndWorld.StrategyPool;

        var container = new StateMachineContainer(pool, ctx);

        Assert.Throws<InvalidOperationException>(() =>
            container.DeserializeWithoutHooks(" ", TestFactory.CreateJsonCodec(), TestFactory.CreateRegistry()));
    }

    [Fact]
    public void StateMachineContainer_SerializeDeserialize_RoundTrip()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var c1 = new StateMachineContainer(pool, ctx);
        var sm = c1.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
        sm.Push("p");
        sm.Push("q");

        var json = c1.SerializeToDataSource(TestFactory.CreateJsonCodec(), TestFactory.CreateRegistry());
        var c2 = new StateMachineContainer(pool, ctx);
        c2.DeserializeWithoutHooks(json, TestFactory.CreateJsonCodec(), TestFactory.CreateRegistry());
        c2.TryGet("ui", out var restored);
        Assert.NotNull(restored);
        Assert.Equal(new[] { "p", "q" }, restored!.Snapshot());

        c1.PopAllOnQuit();
        c2.PopAllOnQuit();
    }
}
