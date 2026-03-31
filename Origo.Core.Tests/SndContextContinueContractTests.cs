using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextContinueContractTests
{
    [Fact]
    public void HasContinueData_WhenNeverSet_ReturnsFalse()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.False(ctx.HasContinueData());
    }

    [Fact]
    public void SetContinueTarget_SetsSystemBlackboardActiveSaveId()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        ctx.SetContinueTarget("123");
        Assert.True(ctx.HasContinueData());

        var (found, id) = ctx.SystemBlackboard.TryGet<string>("origo.active_save_id");
        Assert.True(found);
        Assert.Equal("123", id);
    }

    [Fact]
    public void RequestContinueGame_WhenNoContinueTarget_ReturnsFalse()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.False(ctx.RequestContinueGame());
    }

    [Fact]
    public void ClearContinueTarget_AfterSetContinueTarget_RemovesContinueData()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        ctx.SetContinueTarget("123");
        Assert.True(ctx.HasContinueData());

        ctx.ClearContinueTarget();
        Assert.False(ctx.HasContinueData());
    }
}
