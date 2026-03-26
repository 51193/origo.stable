using System;
using System.Collections.Generic;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
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
            var factory = new RunFactory(logger, fs, "root", runtime, ctx);
            var progress = new Origo.Core.Blackboard.Blackboard();
            var session = new Origo.Core.Blackboard.Blackboard();
            var saveContext = new SaveContext(progress, session, runtime.SndWorld);
            var run = factory.CreateSessionRun(saveContext, "default", session, host);

            var sm = run.SessionScope.StateMachines.CreateOrGet("ui", "sm.push.test", "sm.pop.test");
            sm.Push("a");
            sm.Push("b");

            run.Dispose();

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
    public void StateMachineStrategyEntityAdapter_GetDataOnly_OtherMembersThrow()
    {
        var op = new StateMachineOperationContext("mk", StateMachineDataKeys.OperationPush, null, "top");
        var adapter = new StateMachineStrategyEntityAdapter(op);
        Assert.Equal("top", adapter.GetData<string>(StateMachineDataKeys.AfterTop));
        Assert.Throws<InvalidOperationException>(() => adapter.GetData<int>(StateMachineDataKeys.BeforeTop));
        Assert.Throws<NotSupportedException>(() => adapter.SetData("a", 1));
    }
}
