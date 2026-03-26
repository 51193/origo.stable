using Origo.Core.Runtime;
using Origo.Core.Snd;
using System;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextFlowTests
{
    [Fact]
    public void SndContext_LoadInitialSave_LoadsFromInitialSnapshotAndClearsContinue()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://initial/save_000/progress.json", """{"origo.active_level_id":{"type":"String","data":"default"}}""");
        fs.SeedFile("res://initial/save_000/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("res://initial/save_000/level_default/snd_scene.json", "[]");
        fs.SeedFile("res://initial/save_000/level_default/session.json", "{}");
        fs.SeedFile("res://initial/save_000/level_default/session_state_machines.json", """{"machines":[]}""");

        var ctx = new SndContext(runtime, fs, "user://save", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadInitialSave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.False(ctx.HasContinueData());
    }

    [Fact]
    public void SndContext_RequestSaveGameAuto_GeneratesIdAndListableSnapshot()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var saves = ctx.ListSaves();

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.True(long.TryParse(id, out var ts));
        Assert.InRange(ts, before, after);

        Assert.Contains(id, saves);
        Assert.True(fs.DirectoryExists($"root/save_{id}"));
        Assert.True(fs.Exists($"root/save_{id}/progress.json"));
    }

    [Fact]
    public void SndContext_LoadInitialSave_Throws_WhenStateMachineSnapshotMissing()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host);
        var fs = new TestFileSystem();

        fs.SeedFile("res://initial/save_000/progress.json", """{"origo.active_level_id":{"type":"String","data":"default"}}""");
        // progress_state_machines.json intentionally missing
        fs.SeedFile("res://initial/save_000/level_default/snd_scene.json", "[]");
        fs.SeedFile("res://initial/save_000/level_default/session.json", "{}");
        fs.SeedFile("res://initial/save_000/level_default/session_state_machines.json", """{"machines":[]}""");

        var ctx = new SndContext(runtime, fs, "user://save", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadInitialSave();
        Assert.Throws<InvalidOperationException>(() => ctx.FlushDeferredActionsForCurrentFrame());
    }
}
