using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime.Console;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────
public class SndContextWorkflowTests
{
    /// <summary>Helper: seed a complete save snapshot under root/save_{saveId}/.</summary>
    private static void SeedSaveSnapshot(
        TestFileSystem fs,
        string root,
        string saveId,
        string activeLevelId,
        string progressJson = """{"origo.active_level_id":{"type":"String","data":"default"}}""")
    {
        var saveDir = $"{root}/save_{saveId}";
        var levelDir = $"{saveDir}/level_{activeLevelId}";
        fs.SeedFile($"{saveDir}/progress.json", progressJson);
        fs.SeedFile($"{saveDir}/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile($"{levelDir}/snd_scene.json", "[]");
        fs.SeedFile($"{levelDir}/session.json", "{}");
        fs.SeedFile($"{levelDir}/session_state_machines.json", """{"machines":[]}""");
    }

    private static void SeedInitialSave(TestFileSystem fs, string initialRoot, string levelId = "default")
    {
        var saveDir = $"{initialRoot}/save_000";
        var levelDir = $"{saveDir}/level_{levelId}";
        fs.SeedFile($"{saveDir}/progress.json",
            $$$"""{"origo.active_level_id":{"type":"String","data":"{{{levelId}}}"}}""");
        fs.SeedFile($"{saveDir}/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile($"{levelDir}/snd_scene.json", "[]");
        fs.SeedFile($"{levelDir}/session.json", "{}");
        fs.SeedFile($"{levelDir}/session_state_machines.json", """{"machines":[]}""");
    }

    // ── ListSaves ──

    [Fact]
    public void ListSaves_ReturnsEmptyWhenNoSaves()
    {
        var ctx = CreateContext(out _, out _);
        var saves = ctx.ListSaves();
        Assert.Empty(saves);
    }

    [Fact]
    public void ListSaves_ReturnsSaveIds()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "abc", "default");
        var saves = ctx.ListSaves();
        Assert.Contains("abc", saves);
    }

    // ── RequestSaveGame ──

    [Fact]
    public void RequestSaveGame_ThrowsOnEmptyId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.RequestSaveGame(""));
    }

    [Fact]
    public void RequestSaveGame_PersistsAndSetsActiveSaveSlot()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_slot_01/progress.json"));
        var (found, saveId) = ctx.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal("slot_01", saveId);
    }

    [Fact]
    public void RequestSaveGame_IncrementsThenDecrementsPendingCount()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        ctx.RequestSaveGame("slot_02");
        // Before flush, count should be > 0
        Assert.True(ctx.GetPendingPersistenceRequestCount() > 0);
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
    }

    // ── RequestSaveGameAuto ──

    [Fact]
    public void RequestSaveGameAuto_WithExplicitId_UsesIt()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        var effectiveId = ctx.RequestSaveGameAuto("my_auto");
        Assert.Equal("my_auto", effectiveId);
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.True(fs.Exists("root/save_my_auto/progress.json"));
    }

    [Fact]
    public void RequestSaveGameAuto_WithNullId_GeneratesTimestamp()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        var effectiveId = ctx.RequestSaveGameAuto();
        Assert.False(string.IsNullOrWhiteSpace(effectiveId));
        // Should be parseable as a long (unix timestamp ms)
        Assert.True(long.TryParse(effectiveId, out _));
        ctx.FlushDeferredActionsForCurrentFrame();
    }

    // ── RequestLoadGame ──

    [Fact]
    public void RequestLoadGame_ThrowsOnEmptyId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.RequestLoadGame(""));
    }

    [Fact]
    public void RequestLoadGame_LoadsSaveAndRestoresProgress()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "save1", "default");

        ctx.RequestLoadGame("save1");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.NotNull(ctx.SessionManager.ForegroundSession);
        var (found, saveId) = ctx.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal("save1", saveId);
    }

    [Fact]
    public void RequestLoadGame_IncrementsThenDecrementsPendingCount()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "save2", "default");

        ctx.RequestLoadGame("save2");
        Assert.True(ctx.GetPendingPersistenceRequestCount() > 0);
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
    }

    // ── HasContinueData / SetContinueTarget / RequestContinueGame ──

    [Fact]
    public void HasContinueData_FalseWhenNoTargetSet()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.HasContinueData());
    }

    [Fact]
    public void SetContinueTarget_MakesHasContinueDataTrue()
    {
        var ctx = CreateContext(out _, out _);
        ctx.SetContinueTarget("slot_x");
        Assert.True(ctx.HasContinueData());
    }

    [Fact]
    public void RequestContinueGame_ReturnsFalseWhenNoContinue()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.RequestContinueGame());
    }

    [Fact]
    public void RequestContinueGame_ReturnsTrueAndLoadsWhenContinueSet()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedSaveSnapshot(fs, "root", "cont", "default");
        ctx.SetContinueTarget("cont");

        Assert.True(ctx.RequestContinueGame());
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.NotNull(ctx.SessionManager.ForegroundSession);
    }

    // ── RequestLoadInitialSave ──

    [Fact]
    public void RequestLoadInitialSave_LoadsFromInitialRoot()
    {
        var ctx = CreateContext(out var fs, out _);
        SeedInitialSave(fs, "res://initial");

        ctx.RequestLoadInitialSave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.NotNull(ctx.SessionManager.ForegroundSession);
        // After initial load, active save id should be cleared
        var (found, saveId) = ctx.SystemBlackboard.TryGet<string>(WellKnownKeys.ActiveSaveId);
        Assert.True(found);
        Assert.Equal(string.Empty, saveId);
    }

    // ── RequestSwitchForegroundLevel ──

    [Fact]
    public void RequestSwitchForegroundLevel_ThrowsOnEmptyId()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<ArgumentException>(() => ctx.RequestSwitchForegroundLevel(""));
    }

    [Fact]
    public void RequestSwitchForegroundLevel_SwitchesLevel()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        // Seed target level
        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        fs.SeedFile("root/current/level_b/session_state_machines.json", """{"machines":[]}""");

        ctx.RequestSwitchForegroundLevel("b");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Equal("b", ctx.SessionManager.ForegroundSession?.LevelId);
    }

    // ── CloneTemplate ──

    [Fact]
    public void CloneTemplate_ClonesAndOverridesName()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        SeedTemplate(fs, "tmpl_a", "OriginalName");
        runtime.SndWorld.LoadTemplates(fs, "maps/templates.map", logger);

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "entry.json");
        var cloned = ctx.CloneTemplate("tmpl_a", "NewName");

        Assert.Equal("NewName", cloned.Name);
    }

    [Fact]
    public void CloneTemplate_WithoutOverrideName_KeepsOriginal()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        SeedTemplate(fs, "tmpl_b", "KeepMe");
        runtime.SndWorld.LoadTemplates(fs, "maps/templates.map", logger);

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "entry.json");
        var cloned = ctx.CloneTemplate("tmpl_b");
        Assert.Equal("KeepMe", cloned.Name);
    }

    private static void SeedTemplate(TestFileSystem fs, string alias, string name)
    {
        fs.SeedFile("maps/templates.map", $"{alias}: templates/{alias}.json");
        fs.SeedFile($"templates/{alias}.json",
            $$"""
              {
                "name": "{{name}}",
                "node": { "pairs": {} },
                "strategy": { "indices": [] },
                "data": { "pairs": {} }
              }
              """);
    }

    // ── Console API ──

    [Fact]
    public void TrySubmitConsoleCommand_ReturnsFalseForEmptyCommand()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.TrySubmitConsoleCommand(""));
        Assert.False(ctx.TrySubmitConsoleCommand("   "));
    }

    [Fact]
    public void TrySubmitConsoleCommand_ReturnsTrueWhenConsoleInputExists()
    {
        var ctx = CreateContextWithConsole(out _, out _, out _);
        Assert.True(ctx.TrySubmitConsoleCommand("snd_count"));
    }

    [Fact]
    public void TrySubmitConsoleCommand_ReturnsFalseWhenNoConsoleInput()
    {
        var ctx = CreateContext(out _, out _);
        Assert.False(ctx.TrySubmitConsoleCommand("snd_count"));
    }

    [Fact]
    public void ProcessConsolePending_ProcessesQueuedCommands()
    {
        var ctx = CreateContextWithConsole(out _, out _, out var output);
        var received = new List<string>();
        output.Subscribe(line => received.Add(line));

        ctx.TrySubmitConsoleCommand("snd_count");
        ctx.ProcessConsolePending();

        Assert.Contains(received, s => s.Contains("Snd count:"));
    }

    [Fact]
    public void SubscribeConsoleOutput_ReturnsPositiveId()
    {
        var ctx = CreateContextWithConsole(out _, out _, out _);
        var id = ctx.SubscribeConsoleOutput(_ => { });
        Assert.True(id > 0);
    }

    [Fact]
    public void SubscribeConsoleOutput_ThrowsWhenNoChannel()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Throws<InvalidOperationException>(() => ctx.SubscribeConsoleOutput(_ => { }));
    }

    [Fact]
    public void UnsubscribeConsoleOutput_RemovesSubscription()
    {
        var ctx = CreateContextWithConsole(out _, out _, out var output);
        var received = new List<string>();
        var subId = ctx.SubscribeConsoleOutput(line => received.Add(line));
        ctx.UnsubscribeConsoleOutput(subId);

        output.Publish("test");
        Assert.Empty(received);
    }

    // ── GetPendingPersistenceRequestCount / EnqueueBusinessDeferred ──

    [Fact]
    public void GetPendingPersistenceRequestCount_InitiallyZero()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void EnqueueBusinessDeferred_ExecutesOnFlush()
    {
        var ctx = CreateContext(out _, out _);
        var executed = false;
        ctx.EnqueueBusinessDeferred(() => executed = true);
        Assert.False(executed);
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.True(executed);
    }

    // ── GetProgressStateMachines ──

    [Fact]
    public void GetProgressStateMachines_NullWhenNoProgress()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Null(ctx.GetProgressStateMachines());
    }

    [Fact]
    public void GetProgressStateMachines_NotNullAfterProgressRunCreated()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);
        Assert.NotNull(ctx.GetProgressStateMachines());
    }

    // ── SndContext constructor validation ──

    [Fact]
    public void Constructor_ThrowsOnNullRuntime()
    {
        var fs = new TestFileSystem();
        Assert.Throws<ArgumentNullException>(() => new SndContext(null!, fs, "root", "init", "e.json"));
    }

    [Fact]
    public void Constructor_ThrowsOnNullFileSystem()
    {
        var runtime = TestFactory.CreateRuntime();
        Assert.Throws<ArgumentNullException>(() => new SndContext(runtime, null!, "root", "init", "e.json"));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptySaveRootPath()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestFileSystem();
        Assert.Throws<ArgumentException>(() => new SndContext(runtime, fs, "", "init", "e.json"));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyInitialSaveRootPath()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestFileSystem();
        Assert.Throws<ArgumentException>(() => new SndContext(runtime, fs, "root", "", "e.json"));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyEntryConfigPath()
    {
        var runtime = TestFactory.CreateRuntime();
        var fs = new TestFileSystem();
        Assert.Throws<ArgumentException>(() => new SndContext(runtime, fs, "root", "init", ""));
    }

    // ── SndContext initial state ──

    [Fact]
    public void InitialState_NoProgressBlackboard_NoCurrentSession()
    {
        var ctx = CreateContext(out _, out _);
        Assert.Null(ctx.ProgressBlackboard);
        Assert.Null(ctx.CurrentSession);
        Assert.NotNull(ctx.SystemBlackboard);
        Assert.NotNull(ctx.SessionManager);
        Assert.NotNull(ctx.SndRuntime);
    }

    // ── BeginWorkflow concurrent guard ──

    [Fact]
    public void RequestSaveGame_ConcurrentWorkflow_AllowsSequentialSavesInSingleFlush()
    {
        var ctx = CreateContext(out var fs, out _);
        SetupProgressRun(ctx, fs);

        // Request save twice - second enqueued action should throw
        ctx.RequestSaveGame("slot_a");
        ctx.RequestSaveGame("slot_b");
        // The second call will try BeginWorkflow while first is in progress within same flush
        // Both are enqueued as system deferred; first completes, second runs after
        // Actually both run in same flush, sequentially, so this should succeed
        var ex = Record.Exception(() => ctx.FlushDeferredActionsForCurrentFrame());
        // The first save succeeds and EndWorkflow is called, so the second should also succeed
        Assert.Null(ex);
    }

    // ── Helpers ──

    private static SndContext CreateContext(out TestFileSystem fs, out TestLogger logger)
    {
        logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        fs = new TestFileSystem();
        return new SndContext(runtime, fs, "root", "res://initial", "entry.json");
    }

    private static SndContext CreateContextWithConsole(
        out TestFileSystem fs,
        out TestLogger logger,
        out ConsoleOutputChannel output)
    {
        logger = new TestLogger();
        var host = new TestSndSceneHost();
        var input = new ConsoleInputQueue();
        output = new ConsoleOutputChannel();
        var bb = new Blackboard.Blackboard();
        var tm = new TypeStringMapping();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, input, output);
        fs = new TestFileSystem();
        return new SndContext(runtime, fs, "root", "res://initial", "entry.json");
    }

    private static void SetupProgressRun(SndContext ctx, TestFileSystem fs)
    {
        // Load main menu entry to establish a ProgressRun
        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. NullSndContext — cover all no-op methods
// ─────────────────────────────────────────────────────────────────────────────
public class NullSndContextExtendedTests
{
    [Fact]
    public void ListSaves_ReturnsEmpty() => Assert.Empty(NullSndContext.Instance.ListSaves());

    [Fact]
    public void RequestSaveGameAuto_WithNull_ReturnsEmpty() =>
        Assert.Equal(string.Empty, NullSndContext.Instance.RequestSaveGameAuto());

    [Fact]
    public void RequestSaveGameAuto_WithValue_ReturnsSameValue() =>
        Assert.Equal("my_save", NullSndContext.Instance.RequestSaveGameAuto("my_save"));

    [Fact]
    public void HasContinueData_ReturnsFalse() => Assert.False(NullSndContext.Instance.HasContinueData());

    [Fact]
    public void RequestContinueGame_ReturnsFalse() => Assert.False(NullSndContext.Instance.RequestContinueGame());

    [Fact]
    public void VoidOperations_DoNotChangeObservableState()
    {
        var ex = Record.Exception(() =>
        {
            NullSndContext.Instance.RequestLoadGame("any");
            NullSndContext.Instance.RequestSaveGame("any");
            NullSndContext.Instance.SetContinueTarget("any");
            NullSndContext.Instance.RequestSwitchForegroundLevel("level");
            NullSndContext.Instance.RequestLoadInitialSave();
            NullSndContext.Instance.RequestLoadMainMenuEntrySave();
        });

        Assert.Null(ex);
        Assert.False(NullSndContext.Instance.HasContinueData());
        Assert.Empty(NullSndContext.Instance.ListSaves());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. SessionSndContext — pass-through delegation
// ─────────────────────────────────────────────────────────────────────────────
public class SessionSndContextExtendedTests
{
    [Fact]
    public void ListSaves_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        _ = ctx.ListSaves();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestLoadGame_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.RequestLoadGame("id");
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestSaveGame_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.RequestSaveGame("id");
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestSaveGameAuto_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        var result = ctx.RequestSaveGameAuto("auto_id");
        Assert.Equal("auto_id", result);
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void SetContinueTarget_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.SetContinueTarget("id");
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestSwitchForegroundLevel_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.RequestSwitchForegroundLevel("level");
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void HasContinueData_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        _ = ctx.HasContinueData();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestContinueGame_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        _ = ctx.RequestContinueGame();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestLoadInitialSave_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.RequestLoadInitialSave();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void Constructor_ThrowsOnNullGlobal()
    {
        var session = new StubSessionRun("lv");
        Assert.Throws<ArgumentNullException>(() => new SessionSndContext(null!, session));
    }

    [Fact]
    public void Constructor_ThrowsOnNullSession()
    {
        var global = new TrackingFakeSndContext();
        Assert.Throws<ArgumentNullException>(() => new SessionSndContext(global, null!));
    }

    private static (SessionSndContext ctx, TrackingFakeSndContext global) Create()
    {
        var global = new TrackingFakeSndContext();
        var session = new StubSessionRun("bg_level");
        return (new SessionSndContext(global, session), global);
    }

    private sealed class TrackingFakeSndContext : ISndContext
    {
        public int CallCount { get; private set; }
        public IBlackboard SystemBlackboard { get; } = new Blackboard.Blackboard();
        public IBlackboard? ProgressBlackboard { get; } = new Blackboard.Blackboard();
        public ISessionManager SessionManager { get; } = EmptySessionManager.Instance;
        public ISessionRun? CurrentSession => null;
        public bool IsFrontSession => false;

        public void EnqueueBusinessDeferred(Action action)
        {
            CallCount++;
            action();
        }

        public void FlushDeferredActionsForCurrentFrame() => CallCount++;

        public int GetPendingPersistenceRequestCount()
        {
            CallCount++;
            return 0;
        }

        public SndMetaData CloneTemplate(string templateKey, string? overrideName = null)
        {
            CallCount++;
            return new SndMetaData
            {
                Name = overrideName ?? templateKey, NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(), DataMetaData = new DataMetaData()
            };
        }

        public bool TrySubmitConsoleCommand(string commandLine)
        {
            CallCount++;
            return true;
        }

        public void ProcessConsolePending() => CallCount++;

        public long SubscribeConsoleOutput(Action<string> onLine)
        {
            CallCount++;
            return 1;
        }

        public void UnsubscribeConsoleOutput(long subscriptionId) => CallCount++;

        public StateMachineContainer? GetProgressStateMachines()
        {
            CallCount++;
            return null;
        }

        public IReadOnlyList<string> ListSaves()
        {
            CallCount++;
            return Array.Empty<string>();
        }

        public void RequestLoadGame(string saveId) => CallCount++;
        public void RequestSaveGame(string newSaveId) => CallCount++;

        public string RequestSaveGameAuto(string? newSaveId = null)
        {
            CallCount++;
            return newSaveId ?? "auto";
        }

        public void SetContinueTarget(string saveId) => CallCount++;
        public void RequestSwitchForegroundLevel(string newLevelId) => CallCount++;

        public bool HasContinueData()
        {
            CallCount++;
            return false;
        }

        public bool RequestContinueGame()
        {
            CallCount++;
            return false;
        }

        public void RequestLoadInitialSave() => CallCount++;
        public void RequestLoadMainMenuEntrySave() => CallCount++;
    }

    private sealed class StubSessionRun(string levelId) : ISessionRun
    {
        public IBlackboard SessionBlackboard { get; } = new Blackboard.Blackboard();
        public ISndSceneHost SceneHost => throw new NotImplementedException();
        public string LevelId { get; } = levelId;
        public bool IsFrontSession => false;
        public StateMachineContainer GetSessionStateMachines() => throw new NotImplementedException();

        public void Dispose()
        {
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. MemorySndSceneHost + MemorySndEntity
// ─────────────────────────────────────────────────────────────────────────────
public class MemorySndSceneHostTests
{
    [Fact]
    public void Spawn_AddsEntityAndMeta()
    {
        var host = new MemorySndSceneHost();
        var meta = MakeMeta("e1");
        var entity = host.Spawn(meta);

        Assert.Equal("e1", entity.Name);
        Assert.Single(host.GetEntities());
        Assert.Single(host.SerializeMetaList());
    }

    [Fact]
    public void Spawn_ThrowsOnNull()
    {
        var host = new MemorySndSceneHost();
        Assert.Throws<ArgumentNullException>(() => host.Spawn(null!));
    }

    [Fact]
    public void FindByName_ReturnsEntity()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(MakeMeta("abc"));
        Assert.NotNull(host.FindByName("abc"));
        Assert.Null(host.FindByName("nonexistent"));
    }

    [Fact]
    public void LoadFromMetaList_ReplacesExisting()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(MakeMeta("old"));
        Assert.Single(host.GetEntities());

        host.LoadFromMetaList(new[] { MakeMeta("new1"), MakeMeta("new2") });
        Assert.Equal(2, host.GetEntities().Count);
        Assert.Null(host.FindByName("old"));
        Assert.NotNull(host.FindByName("new1"));
        Assert.NotNull(host.FindByName("new2"));
    }

    [Fact]
    public void LoadFromMetaList_ThrowsOnNull()
    {
        var host = new MemorySndSceneHost();
        Assert.Throws<ArgumentNullException>(() => host.LoadFromMetaList(null!));
    }

    [Fact]
    public void ClearAll_RemovesEntitiesAndMeta()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(MakeMeta("x"));
        host.ClearAll();
        Assert.Empty(host.GetEntities());
        Assert.Empty(host.SerializeMetaList());
    }

    [Fact]
    public void SerializeMetaList_ReturnsCorrectData()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(MakeMeta("a"));
        host.Spawn(MakeMeta("b"));
        var list = host.SerializeMetaList();
        Assert.Equal(2, list.Count);
    }

    private static SndMetaData MakeMeta(string name) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData(),
        DataMetaData = new DataMetaData()
    };
}

public class MemorySndEntityTests
{
    [Fact]
    public void Constructor_ThrowsOnNullName() =>
        Assert.Throws<ArgumentNullException>(() => new MemorySndEntity(null!));

    [Fact]
    public void Name_ReturnsConstructedName()
    {
        var entity = new MemorySndEntity("hero");
        Assert.Equal("hero", entity.Name);
    }

    [Fact]
    public void SetData_GetData_RoundTrip()
    {
        var entity = new MemorySndEntity("e");
        entity.SetData("hp", 100);
        Assert.Equal(100, entity.GetData<int>("hp"));
    }

    [Fact]
    public void GetData_ReturnsDefault_WhenMissing()
    {
        var entity = new MemorySndEntity("e");
        Assert.Equal(0, entity.GetData<int>("missing"));
        Assert.Null(entity.GetData<string>("missing"));
    }

    [Fact]
    public void TryGetData_ReturnsTrueWhenFound()
    {
        var entity = new MemorySndEntity("e");
        entity.SetData("score", 42);
        var (found, value) = entity.TryGetData<int>("score");
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetData_ReturnsFalseWhenMissing()
    {
        var entity = new MemorySndEntity("e");
        var (found, _) = entity.TryGetData<int>("nope");
        Assert.False(found);
    }

    [Fact]
    public void TryGetData_ReturnsFalseForTypeMismatch()
    {
        var entity = new MemorySndEntity("e");
        entity.SetData("val", "string_value");
        var (found, _) = entity.TryGetData<int>("val");
        Assert.False(found);
    }

    [Fact]
    public void SubscribeAndStrategyOperations_KeepDataAndNodeStateStable()
    {
        var entity = new MemorySndEntity("e");
        entity.SetData("hp", 10);
        Action<object?, object?, object?> callback = (_, _, _) => { };

        var ex = Record.Exception(() =>
        {
            entity.Subscribe("prop", callback);
            entity.Unsubscribe("prop", callback);
            entity.AddStrategy("idx1");
            entity.RemoveStrategy("idx1");
        });

        Assert.Null(ex);
        Assert.Equal(10, entity.GetData<int>("hp"));
        Assert.Empty(entity.GetNodeNames());
    }

    [Fact]
    public void GetNode_ThrowsInvalidOperation()
    {
        var entity = new MemorySndEntity("e");
        Assert.Throws<InvalidOperationException>(() => entity.GetNode("node1"));
    }

    [Fact]
    public void GetNodeNames_ReturnsEmpty()
    {
        var entity = new MemorySndEntity("e");
        Assert.Empty(entity.GetNodeNames());
    }

    [Fact]
    public void InitialNameData_IsSetInDictionary()
    {
        var entity = new MemorySndEntity("test_name");
        Assert.Equal("test_name", entity.GetData<string>("name"));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. EntityStrategyBase — virtual hooks coverage
// ─────────────────────────────────────────────────────────────────────────────
public class EntityStrategyBaseTests
{
    [Fact]
    public void DefaultHooks_DoNotMutateEntityData()
    {
        var strategy = new TestEntityStrategy();
        var entity = new MemorySndEntity("e");
        entity.SetData("score", 7);
        ISndContext ctx = NullSndContext.Instance;

        strategy.Process(entity, 0.016, ctx);
        strategy.AfterSpawn(entity, ctx);
        strategy.AfterLoad(entity, ctx);
        strategy.AfterAdd(entity, ctx);
        strategy.BeforeRemove(entity, ctx);
        strategy.BeforeSave(entity, ctx);
        strategy.BeforeQuit(entity, ctx);
        strategy.BeforeDead(entity, ctx);

        Assert.Equal(7, entity.GetData<int>("score"));
    }

    private sealed class TestEntityStrategy : EntityStrategyBase
    {
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. StateMachineStrategyBase — virtual hooks coverage
// ─────────────────────────────────────────────────────────────────────────────
public class StateMachineStrategyBaseTests
{
    [Fact]
    public void DefaultHooks_DoNotScheduleActions()
    {
        var strategy = new TestSmStrategy();
        var smCtx = new StateMachineStrategyContext("machine1", null, "state_a");
        var ctx = new StubStateMachineContext();

        strategy.OnPushRuntime(smCtx, ctx);
        strategy.OnPushAfterLoad(smCtx, ctx);
        strategy.OnPopRuntime(smCtx, ctx);
        strategy.OnPopBeforeQuit(smCtx, ctx);

        Assert.Equal(0, ctx.EnqueueCount);
    }

    private sealed class TestSmStrategy : StateMachineStrategyBase
    {
    }

    private sealed class StubStateMachineContext : IStateMachineContext
    {
        public int EnqueueCount { get; private set; }
        public IBlackboard SystemBlackboard { get; } = new Blackboard.Blackboard();
        public IBlackboard? ProgressBlackboard => null;
        public IBlackboard? SessionBlackboard => null;
        public ISndSceneAccess SceneAccess => throw new NotImplementedException();

        public void EnqueueBusinessDeferred(Action action)
        {
            EnqueueCount++;
            action();
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 7. LevelBuilder — cover builder methods
// ─────────────────────────────────────────────────────────────────────────────
public class LevelBuilderExtendedTests
{
    [Fact]
    public void Build_ProducesLevelPayload()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        builder.AddEntity(MakeMeta("entity_a"));
        builder.SetSessionData("key1", "val1");

        var payload = builder.Build();

        Assert.Equal("lvl1", payload.LevelId);
        Assert.False(string.IsNullOrWhiteSpace(payload.SndSceneJson));
        Assert.False(string.IsNullOrWhiteSpace(payload.SessionJson));
        Assert.False(string.IsNullOrWhiteSpace(payload.SessionStateMachinesJson));
    }

    [Fact]
    public void Build_ThenModify_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.AddEntity(MakeMeta("x")));
        Assert.Throws<InvalidOperationException>(() => builder.SetSessionData("k", 1));
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Commit_WritesToFileSystem()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);
        builder.AddEntity(MakeMeta("e1"));

        var payload = builder.Commit();
        Assert.Equal("lvl1", payload.LevelId);
        Assert.True(fs.Exists("root/current/level_lvl1/snd_scene.json"));
    }

    [Fact]
    public void AddEntity_DuplicateName_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);
        builder.AddEntity(MakeMeta("dup"));

        Assert.Throws<InvalidOperationException>(() => builder.AddEntity(MakeMeta("dup")));
    }

    [Fact]
    public void AddEntity_NullMeta_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        Assert.Throws<ArgumentNullException>(() => builder.AddEntity(null!));
    }

    [Fact]
    public void AddEntity_EmptyName_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        Assert.Throws<ArgumentException>(() => builder.AddEntity(new SndMetaData
        {
            Name = "",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        }));
    }

    [Fact]
    public void AddEntities_BatchAdd()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        builder.AddEntities(new[] { MakeMeta("a"), MakeMeta("b"), MakeMeta("c") });
        Assert.Equal(3, builder.SceneHost.GetEntities().Count);
    }

    [Fact]
    public void AddEntities_NullList_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        Assert.Throws<ArgumentNullException>(() => builder.AddEntities(null!));
    }

    [Fact]
    public void AddEntityFromTemplate_ClonesAndAdds()
    {
        var logger = new TestLogger();
        var fs = new TestFileSystem();
        fs.SeedFile("maps/templates.map", "tmpl: templates/tmpl.json");
        fs.SeedFile("templates/tmpl.json",
            """
            {
              "name": "TemplateEntity",
              "node": { "pairs": {} },
              "strategy": { "indices": [] },
              "data": { "pairs": {} }
            }
            """);
        var sndWorld = TestFactory.CreateSndWorld(logger: logger);
        sndWorld.LoadTemplates(fs, "maps/templates.map", logger);

        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        builder.AddEntityFromTemplate("tmpl", "overridden");
        Assert.NotNull(builder.SceneHost.FindByName("overridden"));
    }

    [Fact]
    public void AddEntityFromTemplate_EmptyKey_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        Assert.Throws<ArgumentException>(() => builder.AddEntityFromTemplate(""));
    }

    [Fact]
    public void Constructor_EmptyLevelId_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        Assert.Throws<ArgumentException>(() => new LevelBuilder("", sndWorld, storage));
    }

    [Fact]
    public void Constructor_NullSndWorld_Throws()
    {
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        Assert.Throws<ArgumentNullException>(() => new LevelBuilder("lvl", null!, storage));
    }

    [Fact]
    public void Constructor_NullStorage_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        Assert.Throws<ArgumentNullException>(() => new LevelBuilder("lvl", sndWorld, null!));
    }

    [Fact]
    public void SessionBlackboard_IsAccessible()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);
        builder.SetSessionData("key", 42);

        var (found, val) = builder.SessionBlackboard.TryGet<int>("key");
        Assert.True(found);
        Assert.Equal(42, val);
    }

    [Fact]
    public void LevelId_ExposesConstructedValue()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var storage = new DefaultSaveStorageService(fs, "root");
        var builder = new LevelBuilder("my_level", sndWorld, storage);
        Assert.Equal("my_level", builder.LevelId);
    }

    private static SndMetaData MakeMeta(string name) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData(),
        DataMetaData = new DataMetaData()
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// 8. SndCountCommandHandler — exercise ExecuteCore via console pipeline
// ─────────────────────────────────────────────────────────────────────────────
public class SndCountCommandHandlerTests
{
    [Fact]
    public void SndCount_PublishesEntityCount()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var input = new ConsoleInputQueue();
        var output = new ConsoleOutputChannel();
        var bb = new Blackboard.Blackboard();
        var tm = new TypeStringMapping();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, input, output);

        // Spawn some entities
        runtime.Snd.Spawn(new SndMetaData
        {
            Name = "e1",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });
        runtime.Snd.Spawn(new SndMetaData
        {
            Name = "e2",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        var received = new List<string>();
        output.Subscribe(line => received.Add(line));

        input.Enqueue("snd_count");
        runtime.Console!.ProcessPending();

        Assert.Contains(received, s => s.Contains("Snd count: 2"));
    }

    [Fact]
    public void SndCount_WithNoEntities_PublishesZero()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var input = new ConsoleInputQueue();
        var output = new ConsoleOutputChannel();
        var bb = new Blackboard.Blackboard();
        var tm = new TypeStringMapping();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, input, output);

        var received = new List<string>();
        output.Subscribe(line => received.Add(line));

        input.Enqueue("snd_count");
        runtime.Console!.ProcessPending();

        Assert.Contains(received, s => s.Contains("Snd count: 0"));
    }
}
