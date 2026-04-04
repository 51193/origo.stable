using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SystemBlackboardPersistenceTests
{
    [Fact]
    public void SndContext_SetContinueTarget_PersistsActiveSaveId_ToSystemJson_AndCanReload()
    {
        var fs = new TestFileSystem();
        var codec = TestFactory.CreateJsonCodec();
        var registry = TestFactory.CreateRegistry();
        var path = "root/system.json";

        var persistent = new PersistentBlackboard(fs, path, codec, registry, new Blackboard.Blackboard());
        var runtime = TestFactory.CreateRuntime(systemBb: persistent);
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");

        ctx.SetContinueTarget("777");

        Assert.True(fs.Exists(path));

        var loadedBoard = new PersistentBlackboard(fs, path, codec, registry, new Blackboard.Blackboard());
        loadedBoard.LoadFromDisk();
        var (found, id) = loadedBoard.TryGet<string>("origo.active_save_id");
        Assert.True(found);
        Assert.Equal("777", id);
    }

    [Fact]
    public void SndContext_SystemBlackboard_IsAlwaysAvailable_BeforeProgressRun()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");

        Assert.NotNull(ctx.SystemBlackboard);
        ctx.SystemBlackboard.Set("test_key", "test_value");
        var (found, value) = ctx.SystemBlackboard.TryGet<string>("test_key");
        Assert.True(found);
        Assert.Equal("test_value", value);
    }

    [Fact]
    public void SndContext_SystemBlackboard_SurvivesProgressRunLifecycle()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        // Set value before any progress run.
        ctx.SystemBlackboard.Set("persist_key", 42);

        // Create and dispose a progress run.
        var progressRun = ctx.RunFactory.CreateProgressRun("save_01", new Blackboard.Blackboard());
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("level_a");
        progressRun.Dispose();
        ctx.SetProgressRun(null);

        // System blackboard value should survive.
        var (found, value) = ctx.SystemBlackboard.TryGet<int>("persist_key");
        Assert.True(found);
        Assert.Equal(42, value);
    }
}
