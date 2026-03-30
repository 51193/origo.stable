using System;
using System.Collections.Generic;
using Origo.Core.Runtime;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextSaveGameContractTests
{
    [Fact]
    public void RequestSaveGame_InvokesMetaContributors_WithCorrectBuildContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        SaveMetaBuildContext? captured = null;
        ctx.RegisterSaveMetaContributor((c, _) => captured = c);

        ctx.RequestSaveGame("001", "000");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(captured.HasValue);
        var c0 = captured!.Value;
        Assert.Equal("001", c0.SaveId);
        Assert.Equal("main_menu", c0.CurrentLevelId);
        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.NotNull(ctx.SessionBlackboard);
        Assert.Same(ctx.ProgressBlackboard, c0.Progress);
        Assert.Same(ctx.SessionBlackboard, c0.Session);
        Assert.Same(runtime.Snd.SceneHost, c0.SceneAccess);
    }

    [Fact]
    public void RequestSaveGame_WritesMetaMapToCurrent_AndSnapshotContainsMetaMap()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.RegisterSaveMetaContributor((_, d) => d["a"] = "from_contributor");
        ctx.RegisterSaveMetaContributor((_, d) => d["a"] = "from_later_contributor");
        var custom = new Dictionary<string, string> { ["a"] = "from_custom", ["b"] = "2" };

        ctx.RequestSaveGame("001", "000", custom);
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/current/meta.map"));
        var currentMetaText = fs.ReadAllText("root/current/meta.map");
        Assert.Contains("a: from_custom", currentMetaText, StringComparison.Ordinal);
        Assert.Contains("b: 2", currentMetaText, StringComparison.Ordinal);

        Assert.True(fs.Exists("root/save_001/meta.map"));
        var snapshotMetaText = fs.ReadAllText("root/save_001/meta.map");
        Assert.Contains("a: from_custom", snapshotMetaText, StringComparison.Ordinal);
        Assert.Contains("b: 2", snapshotMetaText, StringComparison.Ordinal);

        var (found, active) = ctx.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal("001", active);
    }

    [Fact]
    public void RequestSaveGameAuto_UsesActiveSaveIdAsBase_WhenPresent()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.RequestSaveGame("100", "000");
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.True(ctx.HasContinueData());

        var newId = ctx.RequestSaveGameAuto("101");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Equal("101", newId);
        Assert.True(fs.DirectoryExists("root/save_101"));
        Assert.True(fs.Exists("root/save_101/progress.json"));
    }

    [Fact]
    public void RequestSaveGame_WhenNewSaveIdIsWhitespace_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Throws<ArgumentException>(() => ctx.RequestSaveGame(" ", "000"));
    }
}

