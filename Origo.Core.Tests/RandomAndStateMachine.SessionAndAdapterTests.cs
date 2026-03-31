using System.Collections.Generic;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public partial class RandomAndStateMachineTests
{
    [Fact]
    public void SessionRun_Dispose_InvokesPopAllOnQuit_TopToBottom()
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
            var factory = new RunFactory(logger, fs, "root", runtime, ctx);
            var progress = new Blackboard.Blackboard();
            var session = new Blackboard.Blackboard();
            var saveContext = new SaveContext(progress, session, runtime.SndWorld);
            var run = factory.CreateSessionRun(saveContext, "default", session, host);

            var sm = run.SessionScope.StateMachines.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
            sm.Push("a");
            sm.Push("b");

            run.Dispose();

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
    public void StateMachineStrategyContext_HoldsMachineKeyAndStackSnapshot()
    {
        var ctx = new StateMachineStrategyContext("mk", null, "top");
        Assert.Equal("mk", ctx.MachineKey);
        Assert.Null(ctx.BeforeTop);
        Assert.Equal("top", ctx.AfterTop);
    }
}
