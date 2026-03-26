using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;

namespace Origo.Core.Tests;

public partial class RandomAndStateMachineTests
{
    private static void ResetStrategyHooks()
    {
        SmPushStrategy.PushEvents = null;
        SmPushStrategy.AfterLoadEvents = null;
        SmPopStrategy.PopRemoveEvents = null;
        SmPopStrategy.PopQuitEvents = null;
        SmPopOrderProbeStrategy.Events = null;
    }

    [StrategyIndex("sm.push.test")]
    private sealed class SmPushStrategy : BaseSndStrategy
    {
        public static List<string>? PushEvents { get; set; }
        public static List<string>? AfterLoadEvents { get; set; }

        public override void AfterAdd(ISndEntity entity, SndContext ctx)
        {
            var before = entity.GetData<string>(StateMachineDataKeys.BeforeTop);
            var after = entity.GetData<string>(StateMachineDataKeys.AfterTop);
            PushEvents?.Add($"push:after:{before ?? "null"}->{after ?? "null"}");
        }

        public override void AfterLoad(ISndEntity entity, SndContext ctx)
        {
            var before = entity.GetData<string>(StateMachineDataKeys.BeforeTop);
            var after = entity.GetData<string>(StateMachineDataKeys.AfterTop);
            AfterLoadEvents?.Add($"afterload:{before ?? "null"}->{after ?? "null"}");
        }
    }

    [StrategyIndex("sm.pop.test")]
    private sealed class SmPopStrategy : BaseSndStrategy
    {
        public static List<string>? PopRemoveEvents { get; set; }
        public static List<string>? PopQuitEvents { get; set; }

        public override void BeforeRemove(ISndEntity entity, SndContext ctx)
        {
            var before = entity.GetData<string>(StateMachineDataKeys.BeforeTop);
            var after = entity.GetData<string>(StateMachineDataKeys.AfterTop);
            PopRemoveEvents?.Add($"popremove:before:{before ?? "null"}->{after ?? "null"}");
        }

        public override void BeforeQuit(ISndEntity entity, SndContext ctx)
        {
            var before = entity.GetData<string>(StateMachineDataKeys.BeforeTop);
            var after = entity.GetData<string>(StateMachineDataKeys.AfterTop);
            PopQuitEvents?.Add($"popquit:before:{before ?? "null"}->{after ?? "null"}");
        }
    }

    [StrategyIndex("sm.pop.orderprobe")]
    private sealed class SmPopOrderProbeStrategy : BaseSndStrategy
    {
        public static List<string>? Events { get; set; }

        public override void BeforeQuit(ISndEntity entity, SndContext ctx)
        {
            var mk = entity.GetData<string>(StateMachineDataKeys.MachineKey);
            Events?.Add(mk);
        }
    }
}
