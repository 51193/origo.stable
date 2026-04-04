using System;
using System.Collections.Generic;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Xunit;
using Origo.Core.Save.Serialization;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Tests;

public class LifecycleRunsTests
{
    [Fact]
    public void SessionRun_Dispose_ClearsSessionAndScene_ThenThrowsOnAccess()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        session.Set("foo", 1);
        var saveContext = new SaveContext(progress, session, runtime.SndWorld);
        var run = factory.CreateSessionRun(saveContext, "default", session, host);

        run.Dispose();

        // Scene should have been cleared during Dispose.
        Assert.Empty(host.SerializeMetaList());

        // After Dispose, all property access should throw ObjectDisposedException.
        Assert.Throws<ObjectDisposedException>(() => run.SessionBlackboard);
        Assert.Throws<ObjectDisposedException>(() => run.GetSessionStateMachines());
        Assert.Throws<ObjectDisposedException>(() => run.SceneHost);
        Assert.Throws<ObjectDisposedException>(() => ((SessionRun)run).PersistLevelState());
    }

    [Fact]
    public void ProgressRun_LoadFromPayload_RestoresProgressAndSession()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var progressRun = factory.CreateProgressRun("001", new Blackboard.Blackboard());

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressJson = """{"origo.active_level_id":{"type":"String","data":"default"}}""",
            ProgressStateMachinesJson = "{\"machines\":[]}",
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneJson = "[]",
                    SessionJson = """{"x":{"type":"Int32","data":3}}""",
                    SessionStateMachinesJson = "{\"machines\":[]}"
                }
            }
        };

        progressRun.LoadFromPayload(payload);
        var (found, value) = progressRun.ForegroundSession!.SessionBlackboard.TryGet<int>("x");

        Assert.True(found);
        Assert.Equal(3, value);
    }

    [Fact]
    public void ProgressRun_LoadFromPayload_WithEmptyProgressJson_SyncsActiveLevelIdToBlackboard()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var progressRun = factory.CreateProgressRun("001", new Blackboard.Blackboard());

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressJson = "{}",
            ProgressStateMachinesJson = "{\"machines\":[]}",
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneJson = "[]",
                    SessionJson = "{}",
                    SessionStateMachinesJson = "{\"machines\":[]}"
                }
            }
        };

        progressRun.LoadFromPayload(payload);

        var (found, id) = progressRun.ProgressBlackboard.TryGet<string>(WellKnownKeys.ActiveLevelId);
        Assert.True(found);
        Assert.Equal("default", id);
    }

    [Fact]
    public void ProgressRun_SwitchForegroundLevel_PersistsOldSession_AndLoadsNewSessionFromCurrent()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);

        var progressRun = factory.CreateProgressRun("001", new Blackboard.Blackboard());
        progressRun.LoadAndMountForeground("a");

        // Seed target level payload into current/, as SwitchForeground is strict.
        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        fs.SeedFile("root/current/level_b/session_state_machines.json", "{\"machines\":[]}");

        progressRun.SwitchForeground("b");

        Assert.Equal("b", progressRun.ForegroundSession!.LevelId);
        Assert.Equal(ISessionManager.ForegroundKey, ((SessionRun)progressRun.ForegroundSession!).MountKey);

        var (foundActive, activeFromProgress) = progressRun.ProgressBlackboard.TryGet<string>(WellKnownKeys.ActiveLevelId);
        Assert.True(foundActive);
        Assert.Equal("b", activeFromProgress);

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
        Assert.True(fs.Exists("root/current/level_a/session_state_machines.json"));
    }

    [Fact]
    public void ProgressRun_SwitchForegroundLevel_WhenTargetMissing_EntersEmptySessionAndClearsScene()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);

        var progressRun = factory.CreateProgressRun("001", new Blackboard.Blackboard());
        progressRun.LoadAndMountForeground("a");

        // Missing target level payload in current/ → enter empty session and clear scene (README contract).
        runtime.Snd.SceneHost.Spawn(new SndMetaData
            { Name = "Temp", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });
        Assert.NotEmpty(runtime.Snd.SerializeMetaList());

        progressRun.SwitchForeground("b");

        Assert.Empty(runtime.Snd.SerializeMetaList());
        Assert.Equal("b", progressRun.ForegroundSession?.LevelId);
        Assert.NotNull(progressRun.ForegroundSession);
        Assert.Equal("b", progressRun.ForegroundSession!.LevelId);

        var (foundActive, activeFromProgress) = progressRun.ProgressBlackboard.TryGet<string>(WellKnownKeys.ActiveLevelId);
        Assert.True(foundActive);
        Assert.Equal("b", activeFromProgress);

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
    }

    [Fact]
    public void ProgressRun_LoadAndMountForeground_SyncsActiveLevelIdToProgressBlackboard()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);

        var progressRun = factory.CreateProgressRun("001", new Blackboard.Blackboard());
        Assert.False(progressRun.ProgressBlackboard.TryGet<string>(WellKnownKeys.ActiveLevelId).found);

        progressRun.LoadAndMountForeground("dungeon");

        var (found, id) = progressRun.ProgressBlackboard.TryGet<string>(WellKnownKeys.ActiveLevelId);
        Assert.True(found);
        Assert.Equal("dungeon", id);
        Assert.Equal("dungeon", progressRun.ForegroundSession!.LevelId);
    }

    [Fact]
    public void ProgressRun_BuildSavePayload_ThrowsWhenProgressActiveLevelIdDoesNotMatchForeground()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);

        var progressRun = factory.CreateProgressRun("001", new Blackboard.Blackboard());
        progressRun.LoadAndMountForeground("alpha");
        progressRun.ProgressBlackboard.Set(WellKnownKeys.ActiveLevelId, "wrong");

        Assert.Throws<InvalidOperationException>(() => progressRun.BuildSavePayload("new-save-01"));
    }

    [Fact]
    public void SystemRun_LoadOrContinue_WithNullSaveId_FallsBackToActiveSaveId()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var systemRun = factory.CreateSystemRun();

        // Pre-set active save id in system blackboard.
        systemRun.SetActiveSaveSlot("fallback");

        // Seed snapshot for "fallback" save.
        fs.SeedFile("root/save_fallback/progress.json",
            """{"origo.active_level_id":{"type":"String","data":"default"}}""");
        fs.SeedFile("root/save_fallback/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/save_fallback/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/save_fallback/level_default/session.json", "{}");
        fs.SeedFile("root/save_fallback/level_default/session_state_machines.json", """{"machines":[]}""");

        var progress = systemRun.LoadOrContinue(null);

        Assert.NotNull(progress);
        Assert.Equal("fallback", progress!.SaveId);
    }

    [Fact]
    public void SystemRun_LoadOrContinue_WithEmptySaveId_ReturnsNull()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var systemRun = factory.CreateSystemRun();

        // No ActiveSaveId set in system blackboard; empty string provided.
        var progress = systemRun.LoadOrContinue("");

        Assert.Null(progress);
    }

    // ── SessionRun serialization round-trip ──

    [Fact]
    public void SessionRun_SerializeToPayload_RoundTrip_PreservesBlackboardData()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);

        // Create first session and add data
        var bb1 = new Blackboard.Blackboard();
        bb1.Set("score", 42);
        var saveContext1 = factory.CreateSaveContext(new Blackboard.Blackboard(), bb1);
        var run1 = factory.CreateSessionRun(saveContext1, "level1", bb1, host);

        var payload = ((SessionRun)run1).SerializeToPayload();
        Assert.Equal("level1", payload.LevelId);
        Assert.False(string.IsNullOrWhiteSpace(payload.SessionJson));

        // Create second session and load the payload
        var bb2 = new Blackboard.Blackboard();
        var saveContext2 = factory.CreateSaveContext(new Blackboard.Blackboard(), bb2);
        var run2 = factory.CreateSessionRun(saveContext2, "level1", bb2, host);
        ((SessionRun)run2).LoadFromPayload(payload);

        var (found, value) = run2.SessionBlackboard.TryGet<int>("score");
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void SessionRun_LoadFromPayload_WithEmptyFields_DoesNotThrow()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var bb = new Blackboard.Blackboard();
        var saveContext = factory.CreateSaveContext(new Blackboard.Blackboard(), bb);
        var run = factory.CreateSessionRun(saveContext, "level1", bb, host);

        // Empty fields should be silently skipped (per SessionRun.LoadFromPayload logic)
        var emptyPayload = new LevelPayload
        {
            LevelId = "level1",
            SndSceneJson = "",
            SessionJson = "",
            SessionStateMachinesJson = ""
        };

        var exception = Record.Exception(() => ((SessionRun)run).LoadFromPayload(emptyPayload));
        Assert.Null(exception);
    }

    [Fact]
    public void ProgressRun_LoadFromPayload_MissingStateMachinesJson_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var progressRun = factory.CreateProgressRun("001", new Blackboard.Blackboard());

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressJson = """{"origo.active_level_id":{"type":"String","data":"default"}}""",
            ProgressStateMachinesJson = null!  // Missing — should throw
        };

        Assert.Throws<InvalidOperationException>(() => progressRun.LoadFromPayload(payload));
    }

    // ── New: Mount key tracking tests ──────────────────────────────────

    [Fact]
    public void SessionRun_MountKey_IsNull_WhenNotMounted()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var session = new Blackboard.Blackboard();
        var saveContext = new SaveContext(new Blackboard.Blackboard(), session, runtime.SndWorld);
        var run = factory.CreateSessionRun(saveContext, "default", session, host);

        Assert.Null(((SessionRun)run).MountKey);

        run.Dispose();
    }

    [Fact]
    public void SessionRun_MountKey_SetOnMount_ClearedOnUnmount()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var progressRun = sndContext.RunFactory.CreateProgressRun("001", new Blackboard.Blackboard());
        sndContext.SetProgressRun(progressRun);

        var bg = sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");

        Assert.Equal("bg1", ((SessionRun)bg).MountKey);

        sndContext.SessionManager.DestroySession("bg1");
        Assert.False(sndContext.SessionManager.Contains("bg1"));
    }

    [Fact]
    public void SessionRun_Dispose_AutoUnmountsFromManager()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var progressRun = sndContext.RunFactory.CreateProgressRun("001", new Blackboard.Blackboard());
        sndContext.SetProgressRun(progressRun);

        var bg = sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");
        Assert.True(sndContext.SessionManager.Contains("bg1"));

        bg.Dispose();

        // After Dispose, session should have auto-unmounted.
        Assert.False(sndContext.SessionManager.Contains("bg1"));
    }

    // ── New: LoadAndMountForeground tests ──────────────────────────────

    [Fact]
    public void LoadAndMountForeground_WhenNoPayloadFound_MountsEmptySession()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var progressRun = sndContext.RunFactory.CreateProgressRun("001", new Blackboard.Blackboard());
        sndContext.SetProgressRun(progressRun);

        // No data seeded — should mount empty session.
        var session = progressRun.LoadAndMountForeground("missing_level");

        Assert.NotNull(session);
        Assert.Equal("missing_level", session.LevelId);
        Assert.NotNull(progressRun.SessionManager.ForegroundSession);
    }

    // ── New: ResolveLevelPayload tests ──────────────────────────────────

    [Fact]
    public void ResolveLevelPayload_ReturnsNull_WhenNoData()
    {
        var fs = new TestFileSystem();
        var service = new Save.Storage.DefaultSaveStorageService(fs, "root");

        var result = service.ResolveLevelPayload("001", "nonexistent");

        Assert.Null(result);
    }

    // ── New: Lifecycle logging tests ───────────────────────────────────

    [Fact]
    public void SessionRun_Create_LogsCreation()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var session = new Blackboard.Blackboard();
        var saveContext = new SaveContext(new Blackboard.Blackboard(), session, runtime.SndWorld);

        var run = factory.CreateSessionRun(saveContext, "test_level", session, host);

        Assert.Contains(logger.Infos, msg => msg.Contains("Created SessionRun") && msg.Contains("test_level"));

        run.Dispose();
    }

    [Fact]
    public void ProgressRun_Create_LogsCreation()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");

        var progressRun = sndContext.RunFactory.CreateProgressRun("test_save", new Blackboard.Blackboard());

        Assert.Contains(logger.Infos, msg => msg.Contains("Created ProgressRun") && msg.Contains("test_save"));

        progressRun.Dispose();
    }

    [Fact]
    public void SessionManager_Mount_LogsMounting()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var progressRun = sndContext.RunFactory.CreateProgressRun("001", new Blackboard.Blackboard());
        sndContext.SetProgressRun(progressRun);

        using var bg = sndContext.SessionManager.CreateBackgroundSession("bg1", "bg1");

        Assert.Contains(logger.Infos, msg => msg.Contains("Mounted session") && msg.Contains("bg1"));
    }

    // ── RunFactory parameter validation for background sessions ──

    [Fact]
    public void RunFactory_CreateBackgroundSession_NullStateMachineContext_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        // RunFactory without StateMachineContext
        var factory = new RunFactory(logger, fs, "root", runtime);

        Assert.Throws<InvalidOperationException>(() => factory.CreateBackgroundSession("bg"));
    }

    [Fact]
    public void RunFactory_CreateBackgroundSessionFromPayload_NullStateMachineContext_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var factory = new RunFactory(logger, fs, "root", runtime);
        var payload = new LevelPayload { LevelId = "bg" };

        Assert.Throws<InvalidOperationException>(() => factory.CreateBackgroundSessionFromPayload("bg", payload));
    }

    [Fact]
    public void RunFactory_CreateBackgroundSession_NullSndContext_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        // Factory with StateMachineContext but null SndContext
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext, sndContext: null);

        Assert.Throws<InvalidOperationException>(() => factory.CreateBackgroundSession("bg"));
    }
}
