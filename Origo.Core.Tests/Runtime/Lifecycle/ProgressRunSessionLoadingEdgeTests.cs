using System;
using System.Collections.Generic;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class ProgressRunSessionLoadingEdgeTests
{
    [Fact]
    public void LoadFromPayload_WhenTopologyMalformed_ThrowsInvalidOperation()
    {
        var progressRun = CreateProgressRun();

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressJson = """{"origo.session_topology":{"type":"String","data":"bad_entry"}}""",
            ProgressStateMachinesJson = "{\"machines\":[]}",
            Levels = new Dictionary<string, LevelPayload>()
        };

        var ex = Assert.Throws<InvalidOperationException>(() => progressRun.LoadFromPayload(payload));
        Assert.Contains("Malformed session topology entry", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromPayload_WhenTopologyMissing_ThrowsInvalidOperation()
    {
        var progressRun = CreateProgressRun();

        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressJson = "{}",
            ProgressStateMachinesJson = "{\"machines\":[]}",
            Levels = new Dictionary<string, LevelPayload>()
        };

        var ex = Assert.Throws<InvalidOperationException>(() => progressRun.LoadFromPayload(payload));
        Assert.Contains(WellKnownKeys.SessionTopology, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadAndMountForeground_WhenSndSceneIsEmpty_ThrowsInvalidOperation()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);

        fs.SeedFile("root/current/level_target/snd_scene.json", " ");
        fs.SeedFile("root/current/level_target/session.json", "{}");
        fs.SeedFile("root/current/level_target/session_state_machines.json", "{\"machines\":[]}");

        var ex = Assert.Throws<InvalidOperationException>(() => progressRun.LoadAndMountForeground("target"));
        Assert.Contains("invalid snd_scene.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadAndMountForeground_WhenSessionStateMachineJsonIsMalformed_Throws()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var progressRun = TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);

        fs.SeedFile("root/current/level_target/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_target/session.json", "{}");
        fs.SeedFile("root/current/level_target/session_state_machines.json", "{");

        Assert.ThrowsAny<Exception>(() => progressRun.LoadAndMountForeground("target"));
    }

    private static ProgressRun CreateProgressRun()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        return TestFactory.CreateProgressRun("001", logger, fs, "root", runtime, sndContext);
    }
}
