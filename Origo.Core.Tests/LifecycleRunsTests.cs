using System;
using System.Collections.Generic;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class LifecycleRunsTests
{
    [Fact]
    public void SessionRun_Dispose_ClearsSessionAndScene_ThenThrowsOnAccess()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var progress = new Origo.Core.Blackboard.Blackboard();
        var session = new Origo.Core.Blackboard.Blackboard();
        session.Set("foo", 1);
        var saveContext = new SaveContext(progress, session, runtime.SndWorld);
        var run = factory.CreateSessionRun(saveContext, "default", session, host);

        run.Dispose();

        // Scene should have been cleared during Dispose.
        Assert.Empty(host.SerializeMetaList());

        // After Dispose, all property access should throw ObjectDisposedException.
        Assert.Throws<ObjectDisposedException>(() => run.SessionBlackboard);
        Assert.Throws<ObjectDisposedException>(() => run.SessionScope);
        Assert.Throws<ObjectDisposedException>(() => run.SceneAccess);
        Assert.Throws<ObjectDisposedException>(() => run.PersistLevelState());
    }

    [Fact]
    public void ProgressRun_LoadFromPayload_RestoresProgressAndSession()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var progressRun = factory.CreateProgressRun("001", "default", new Origo.Core.Blackboard.Blackboard());

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
        var (found, value) = progressRun.CurrentSession!.SessionBlackboard.TryGet<int>("x");

        Assert.True(found);
        Assert.Equal(3, value);
    }

    [Fact]
    public void ProgressRun_SwitchLevel_PersistsOldSession_AndLoadsNewSessionFromCurrent()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);

        var progressRun = factory.CreateProgressRun("001", "a", new Origo.Core.Blackboard.Blackboard());
        progressRun.CreateFromAlreadyLoadedScene();

        // Seed target level payload into current/, as SwitchLevel is strict.
        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        fs.SeedFile("root/current/level_b/session_state_machines.json", "{\"machines\":[]}");

        progressRun.SwitchLevel("b");

        Assert.Equal("b", progressRun.ActiveLevelId);
        Assert.Equal("b", progressRun.CurrentSession!.LevelId);

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
        Assert.True(fs.Exists("root/current/level_a/session_state_machines.json"));
    }

    [Fact]
    public void ProgressRun_SwitchLevel_WhenTargetMissing_EntersEmptySessionAndClearsScene()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);

        var progressRun = factory.CreateProgressRun("001", "a", new Origo.Core.Blackboard.Blackboard());
        progressRun.CreateFromAlreadyLoadedScene();

        // Missing target level payload in current/ → enter empty session and clear scene (README contract).
        runtime.Snd.SceneHost.Spawn(new SndMetaData { Name = "Temp", NodeMetaData = new NodeMetaData(), StrategyMetaData = new StrategyMetaData() });
        Assert.NotEmpty(runtime.Snd.SerializeMetaList());

        progressRun.SwitchLevel("b");

        Assert.Empty(runtime.Snd.SerializeMetaList());
        Assert.Equal("b", progressRun.ActiveLevelId);
        Assert.NotNull(progressRun.CurrentSession);
        Assert.Equal("b", progressRun.CurrentSession!.LevelId);

        Assert.True(fs.Exists("root/current/progress.json"));
        Assert.True(fs.Exists("root/current/progress_state_machines.json"));
    }
}
