using System;
using System.Collections.Generic;
using Origo.Core.Runtime;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Serialization;
using Origo.Core.Snd;
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
        var runtime = new OrigoRuntime(logger, host);
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
                    "push:after:null->a",
                    "push:after:a->b",
                    "popremove:before:b->a",
                    "popremove:before:a->null"
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
        var runtime = new OrigoRuntime(logger, host);
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
                    "push:after:null->a",
                    "push:after:a->b",
                    "popquit:before:b->a",
                    "popquit:before:a->null"
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
        var runtime = new OrigoRuntime(logger, host);
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
    public void StateMachineContainer_ImportWithoutHooks_ThrowsOnEmptyJson()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var pool = runtime.SndWorld.StrategyPool;

        var options = OrigoJson.CreateDefaultOptions(runtime.SndWorld.TypeMapping, _ => { });
        var container = new StateMachineContainer(pool, ctx);

        Assert.Throws<InvalidOperationException>(() => container.ImportWithoutHooks(" ", options));
    }

    [Fact]
    public void StateMachineContainer_ExportImport_RoundTrip()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStrategy());
        pool.Register(() => new SmPopStrategy());

        var options = OrigoJson.CreateDefaultOptions(runtime.SndWorld.TypeMapping, _ => { });
        var c1 = new StateMachineContainer(pool, ctx);
        var sm = c1.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
        sm.Push("p");
        sm.Push("q");

        var json = c1.ExportToJson(options);
        var c2 = new StateMachineContainer(pool, ctx);
        c2.ImportWithoutHooks(json, options);
        c2.TryGet("ui", out var restored);
        Assert.NotNull(restored);
        Assert.Equal(new[] { "p", "q" }, restored!.Snapshot());

        c1.PopAllOnQuit();
        c2.PopAllOnQuit();
    }
}
