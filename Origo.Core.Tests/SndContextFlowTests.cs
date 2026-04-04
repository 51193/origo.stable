using System;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextFlowTests
{
    [Fact]
    public void SndContext_Blackboards_NullBeforeProgressRun()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.Null(ctx.ProgressBlackboard);
        Assert.Null(ctx.SessionManager.ForegroundSession);
    }

    [Fact]
    public void SndContext_LoadInitialSave_LoadsFromInitialSnapshotAndClearsContinue()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://initial/save_000/progress.json",
            """{"origo.active_level_id":{"type":"String","data":"default"}}""");
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
        var runtime = TestFactory.CreateRuntime(logger, host);
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

        var progressText = fs.ReadAllText($"root/save_{id}/progress.json");
        Assert.Contains(WellKnownKeys.ActiveLevelId, progressText);
    }

    [Fact]
    public void SndContext_LoadInitialSave_Throws_WhenStateMachineSnapshotMissing()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();

        fs.SeedFile("res://initial/save_000/progress.json",
            """{"origo.active_level_id":{"type":"String","data":"default"}}""");
        // progress_state_machines.json intentionally missing
        fs.SeedFile("res://initial/save_000/level_default/snd_scene.json", "[]");
        fs.SeedFile("res://initial/save_000/level_default/session.json", "{}");
        fs.SeedFile("res://initial/save_000/level_default/session_state_machines.json", """{"machines":[]}""");

        var ctx = new SndContext(runtime, fs, "user://save", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadInitialSave();
        Assert.Throws<InvalidOperationException>(() => ctx.FlushDeferredActionsForCurrentFrame());
    }

    [Fact]
    public void SndContext_WorkflowGuard_PreventsReentrantWorkflow()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        // Two sequential saves should not trigger the reentrant guard.
        ctx.RequestSaveGame("001", "000");
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.RequestSaveGame("002", "001");
        ctx.FlushDeferredActionsForCurrentFrame();

        // Both snapshots must exist.
        Assert.True(fs.DirectoryExists("root/save_001"));
        Assert.True(fs.DirectoryExists("root/save_002"));
    }

    // ── TrySubmitConsoleCommand ──

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SndContext_TrySubmitConsoleCommand_EmptyOrNullInput_ReturnsFalse(string? input)
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.False(ctx.TrySubmitConsoleCommand(input!));
    }

    [Fact]
    public void SndContext_TrySubmitConsoleCommand_WithoutConsoleInput_ReturnsFalse()
    {
        // Default runtime has no ConsoleInput, so TrySubmit returns false even for valid input
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.False(ctx.TrySubmitConsoleCommand("help"));
    }

    [Fact]
    public void SndContext_TrySubmitConsoleCommand_WithConsoleInput_ReturnsTrue()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var consoleInput = new Origo.Core.Runtime.Console.ConsoleInputQueue();
        var consoleOutput = new Origo.Core.Runtime.Console.ConsoleOutputChannel();
        var runtime = TestFactory.CreateRuntime(
            logger, host, new Origo.Core.Serialization.TypeStringMapping(),
            new Blackboard.Blackboard(), consoleInput, consoleOutput);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.True(ctx.TrySubmitConsoleCommand("help"));
    }

    // ── SubscribeConsoleOutput / UnsubscribeConsoleOutput ──

    [Fact]
    public void SndContext_SubscribeConsoleOutput_ReceivesPublishedMessages()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var consoleInput = new Origo.Core.Runtime.Console.ConsoleInputQueue();
        var consoleOutput = new Origo.Core.Runtime.Console.ConsoleOutputChannel();
        var runtime = TestFactory.CreateRuntime(
            logger, host, new Origo.Core.Serialization.TypeStringMapping(),
            new Blackboard.Blackboard(), consoleInput, consoleOutput);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        var received = new System.Collections.Generic.List<string>();
        var subId = ctx.SubscribeConsoleOutput(line => received.Add(line));
        Assert.True(subId > 0);

        consoleOutput.Publish("hello");
        consoleOutput.Publish("world");

        Assert.Equal(2, received.Count);
        Assert.Equal("hello", received[0]);
        Assert.Equal("world", received[1]);

        ctx.UnsubscribeConsoleOutput(subId);
        consoleOutput.Publish("after unsub");
        Assert.Equal(2, received.Count); // no new message
    }

    [Fact]
    public void SndContext_SubscribeConsoleOutput_WithoutChannel_Throws()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.Throws<InvalidOperationException>(() => ctx.SubscribeConsoleOutput(_ => { }));
    }

    [Fact]
    public void SndContext_UnsubscribeConsoleOutput_ZeroOrNegativeId_DoesNotThrow()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        var ex = Record.Exception(() => ctx.UnsubscribeConsoleOutput(0));
        Assert.Null(ex);
        ex = Record.Exception(() => ctx.UnsubscribeConsoleOutput(-1));
        Assert.Null(ex);
    }
}
