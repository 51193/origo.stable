using System.Collections.Generic;
using Origo.Core.Runtime;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextDeferredExecutionTests
{
    [Fact]
    public void FlushDeferredActionsForCurrentFrame_ExecutesBusinessBeforeSystem()
    {
        var logger = new TestLogger();
        var runtime = new OrigoRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        var order = new List<string>();

        ctx.EnqueueSystemDeferred(() => order.Add("system"));
        ctx.EnqueueBusinessDeferred(() =>
        {
            order.Add("business");
            ctx.EnqueueSystemDeferred(() => order.Add("system_nested"));
        });

        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Equal(["business", "system", "system_nested"], order);
    }

    [Fact]
    public void RequestSaveGame_OnlyExecutesOnFlush()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.RequestSaveGame("001", "000");
        Assert.Equal(1, ctx.GetPendingPersistenceRequestCount());
        Assert.False(fs.DirectoryExists("root/save_001"));

        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
        Assert.True(fs.DirectoryExists("root/save_001"));
    }
}
