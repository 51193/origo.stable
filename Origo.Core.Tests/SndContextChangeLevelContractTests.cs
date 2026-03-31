using System;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextChangeLevelContractTests
{
    [Fact]
    public void RequestChangeLevel_WhenTargetHasCompletePayload_LoadsTargetAndUpdatesActiveLevel()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        fs.SeedFile("root/current/level_b/session_state_machines.json", """{"machines":[]}""");

        ctx.RequestChangeLevel("b");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.ProgressBlackboard);
        var (found, activeLevel) = ctx.ProgressBlackboard.TryGet<string>("origo.active_level_id");
        Assert.True(found);
        Assert.Equal("b", activeLevel);
    }

    [Fact]
    public void RequestChangeLevel_WhenTargetPayloadMissing_ClearsSceneAndEntersNewLevelWithoutThrowing()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        // Add some scene state we can observe being cleared.
        runtime.Snd.SceneHost.Spawn(new SndMetaData
            { Name = "Temp", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });
        Assert.NotEmpty(runtime.Snd.SerializeMetaList());

        // level_c payload not seeded in current/ => should clear and enter empty session per README contract.
        ctx.RequestChangeLevel("c");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Empty(runtime.Snd.SerializeMetaList());
        Assert.NotNull(ctx.ProgressBlackboard);
        var (found, activeLevel) = ctx.ProgressBlackboard.TryGet<string>("origo.active_level_id");
        Assert.True(found);
        Assert.Equal("c", activeLevel);
    }

    [Fact]
    public void RequestChangeLevel_WhenTargetPayloadPartiallyMissing_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        // session_state_machines.json intentionally missing (strict mode: treated as corrupted payload)

        ctx.RequestChangeLevel("b");
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushDeferredActionsForCurrentFrame());
        Assert.Contains("session_state_machines.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestChangeLevel_WhenNewLevelIdIsWhitespace_Throws()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.Throws<ArgumentException>(() => ctx.RequestChangeLevel(" "));
    }
}
