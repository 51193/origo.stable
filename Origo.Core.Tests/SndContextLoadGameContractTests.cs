using System;
using Origo.Core.Runtime;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextLoadGameContractTests
{
    [Fact]
    public void RequestLoadGame_ReadsActiveLevelFromProgressJson_WritesCurrent_AndSetsActiveSaveId()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();

        fs.SeedFile("root/save_777/progress.json",
            """{"origo.active_level_id":{"type":"String","data":"level_x"}}""");
        fs.SeedFile("root/save_777/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/save_777/level_level_x/snd_scene.json", "[]");
        fs.SeedFile("root/save_777/level_level_x/session.json", "{}");
        fs.SeedFile("root/save_777/level_level_x/session_state_machines.json", """{"machines":[]}""");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        ctx.RequestLoadGame("777");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
        Assert.True(fs.Exists("root/current/level_level_x/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_level_x/session_state_machines.json"));

        var (found, activeSaveId) = ctx.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal("777", activeSaveId);

        Assert.NotNull(ctx.ProgressBlackboard);
        var (foundLevel, activeLevelId) = ctx.ProgressBlackboard.TryGet<string>(WellKnownKeys.ActiveLevelId);
        Assert.True(foundLevel);
        Assert.Equal("level_x", activeLevelId);
    }

    [Fact]
    public void RequestContinueGame_WhenContinueTargetPresent_LoadsAndReturnsTrue()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();

        fs.SeedFile("root/save_001/progress.json",
            """{"origo.active_level_id":{"type":"String","data":"default"}}""");
        fs.SeedFile("root/save_001/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/save_001/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/save_001/level_default/session.json", "{}");
        fs.SeedFile("root/save_001/level_default/session_state_machines.json", """{"machines":[]}""");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.SetContinueTarget("001");

        Assert.True(ctx.RequestContinueGame());
        ctx.FlushDeferredActionsForCurrentFrame();
        var (found, activeSaveId) = ctx.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal("001", activeSaveId);
    }

    [Fact]
    public void RequestLoadGame_WhenProgressMissingActiveLevelId_Throws()
    {
        var logger = new TestLogger();
        var runtime = new OrigoRuntime(logger, new TestSndSceneHost(), new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();

        fs.SeedFile("root/save_001/progress.json", "{}");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        ctx.RequestLoadGame("001");
        Assert.Throws<InvalidOperationException>(() => ctx.FlushDeferredActionsForCurrentFrame());
    }
}

