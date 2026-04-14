using System;
using System.Collections.Generic;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
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

    [Fact]
    public void StateMachineContainer_DeserializeWithoutHooks_SwapsAtomically()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SwapTestPushStrategy());
        pool.Register(() => new SwapTestPopStrategy());

        var container = new StateMachineContainer(pool, ctx);

        // Serialize an empty container, then deserialize back – no-op swap.
        var emptyJson = container.SerializeToDataSource(TestFactory.CreateJsonCodec(), TestFactory.CreateRegistry());
        container.DeserializeWithoutHooks(emptyJson, TestFactory.CreateJsonCodec(), TestFactory.CreateRegistry());
        Assert.False(container.TryGet("anything", out _));

        // Populate with a machine, serialize, then deserialize into the same container.
        var sm = container.CreateOrGet("nav", "sm.swap.push", "sm.swap.pop");
        sm.RestoreStackWithoutHooks(new List<string> { "a", "b" });
        var json = container.SerializeToDataSource(TestFactory.CreateJsonCodec(), TestFactory.CreateRegistry());

        // Deserialize replaces old state atomically.
        container.DeserializeWithoutHooks(json, TestFactory.CreateJsonCodec(), TestFactory.CreateRegistry());

        Assert.True(container.TryGet("nav", out var restored));
        Assert.NotNull(restored);
        Assert.Equal(new[] { "a", "b" }, restored!.Snapshot());

        // Old key must still be accessible (same key name).
        Assert.True(container.TryGet("nav", out _));

        container.Clear();
    }

    [Fact]
    public void StateMachineContainer_CreateOrGet_IdempotentForSameKeyAndIndices()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(runtime, new TestFileSystem(), "r", "i", "e.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        try
        {
            var c = new StateMachineContainer(pool, ctx);
            var a = c.CreateOrGet("k", "sm.push.test", "sm.pop.test");
            var b = c.CreateOrGet("k", "sm.push.test", "sm.pop.test");
            Assert.Same(a, b);
            c.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_CreateOrGet_ConflictingIndices_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(runtime, new TestFileSystem(), "r", "i", "e.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        pool.Register(() => new SmPopOrderProbeStrategy());
        try
        {
            var c = new StateMachineContainer(pool, ctx);
            c.CreateOrGet("k", "sm.push.test", "sm.pop.test");
            Assert.Throws<InvalidOperationException>(() =>
                c.CreateOrGet("k", "sm.push.test", "sm.pop.orderprobe"));
            c.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_Remove_MissingKey_NoThrow()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(runtime, new TestFileSystem(), "r", "i", "e.json");
        var pool = runtime.SndWorld.StrategyPool;
        var c = new StateMachineContainer(pool, ctx);
        var ex = Record.Exception(() => c.Remove("none"));
        Assert.Null(ex);
    }

    [Fact]
    public void StateMachineContainer_FlushAllAfterLoad_NotifiesPushStrategy()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(runtime, new TestFileSystem(), "r", "i", "e.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        var after = new List<string>();
        SmPushStrategy.AfterLoadEvents = after;
        try
        {
            var c = new StateMachineContainer(pool, ctx);
            var sm = c.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
            sm.RestoreStackWithoutHooks(new List<string> { "x", "y" });
            c.FlushAllAfterLoad();
            Assert.Equal(
                new[] { "push:afterload:null->x", "push:afterload:x->y" },
                after);
            c.Clear();
        }
        finally
        {
            ResetStrategyHooks();
        }
    }

    [Fact]
    public void StateMachineContainer_DeserializeWithoutHooks_NullSerializedText_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(runtime, new TestFileSystem(), "r", "i", "e.json");
        var c = new StateMachineContainer(runtime.SndWorld.StrategyPool, ctx);
        Assert.Throws<ArgumentNullException>(() =>
            c.DeserializeWithoutHooks(null!, TestFactory.CreateJsonCodec(), TestFactory.CreateRegistry()));
    }

    [Fact]
    public void StateMachineContainer_DeserializeWithoutHooks_DuplicateMachineKey_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(runtime, new TestFileSystem(), "r", "i", "e.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());
        var json =
            """
            {
              "machines": [
                { "key": "dup", "pushIndex": "sm.push.test", "popIndex": "sm.pop.test", "stack": [] },
                { "key": "dup", "pushIndex": "sm.push.test", "popIndex": "sm.pop.test", "stack": [] }
              ]
            }
            """;
        var c = new StateMachineContainer(pool, ctx);
        Assert.Throws<InvalidOperationException>(() =>
            c.DeserializeWithoutHooks(json, TestFactory.CreateJsonCodec(), TestFactory.CreateRegistry()));
    }

    [StrategyIndex("sm.swap.push")]
    private sealed class SwapTestPushStrategy : StateMachineStrategyBase
    {
    }

    [StrategyIndex("sm.swap.pop")]
    private sealed class SwapTestPopStrategy : StateMachineStrategyBase
    {
    }
}
