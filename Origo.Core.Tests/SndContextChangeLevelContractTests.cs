using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Tests;

public class SndContextChangeLevelContractTests
{
    [Fact]
    public void RequestSwitchForegroundLevel_WhenTargetHasCompletePayload_LoadsTargetAndUpdatesActiveLevel()
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

        ctx.RequestSwitchForegroundLevel("b");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.Equal("b", ctx.SessionManager.ForegroundSession?.LevelId);
    }

    [Fact]
    public void RequestSwitchForegroundLevel_WhenTargetPayloadMissing_ClearsSceneAndEntersNewLevelWithoutThrowing()
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
        ctx.RequestSwitchForegroundLevel("c");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Empty(runtime.Snd.SerializeMetaList());
        Assert.NotNull(ctx.ProgressBlackboard);
        Assert.Equal("c", ctx.SessionManager.ForegroundSession?.LevelId);
    }

    [Fact]
    public void RequestSwitchForegroundLevel_WhenTargetPayloadPartiallyMissing_Throws()
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

        ctx.RequestSwitchForegroundLevel("b");
        var ex = Assert.Throws<InvalidOperationException>(() => ctx.FlushDeferredActionsForCurrentFrame());
        Assert.Contains("session_state_machines.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestSwitchForegroundLevel_WhenNewLevelIdIsWhitespace_Throws()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.Throws<ArgumentException>(() => ctx.RequestSwitchForegroundLevel(" "));
    }

    [Fact]
    public void ProgressRun_SwitchForegroundLevel_WhenPersistProgressFails_RollsBackActiveLevel()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new FailOnWriteFileSystem("root/current/progress.json");
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);

        var progressRun = factory.CreateProgressRun("001", new Blackboard.Blackboard());
        progressRun.LoadAndMountForeground("a");

        // Seed a valid target level so validation passes.
        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        fs.SeedFile("root/current/level_b/session_state_machines.json", """{"machines":[]}""");

        // SwitchForeground should fail during PersistProgress (writing progress.json).
        Assert.ThrowsAny<Exception>(() => progressRun.SwitchForeground("b"));

        // Foreground session must still point to the original level.
        Assert.Equal("a", progressRun.ForegroundSession?.LevelId);
    }

    /// <summary>
    ///     A file system that delegates to <see cref="TestFileSystem" /> but throws on
    ///     <see cref="IFileSystem.WriteAllText" /> when the path contains the configured fail path.
    ///     Used to simulate persistence failures during SwitchForegroundLevel.
    /// </summary>
    private sealed class FailOnWriteFileSystem : IFileSystem
    {
        private readonly string _failPath;
        private readonly TestFileSystem _inner = new();

        public FailOnWriteFileSystem(string failPath)
        {
            _failPath = failPath;
        }

        public bool Exists(string path) => _inner.Exists(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public string ReadAllText(string path) => _inner.ReadAllText(path);

        public void WriteAllText(string path, string content, bool overwrite)
        {
            if (path.Replace('\\', '/').Contains(_failPath, StringComparison.Ordinal))
                throw new InvalidOperationException($"Simulated write failure for '{path}'.");
            _inner.WriteAllText(path, content, overwrite);
        }

        public void Copy(string sourcePath, string destinationPath, bool overwrite)
            => _inner.Copy(sourcePath, destinationPath, overwrite);

        public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
            => _inner.EnumerateFiles(directoryPath, searchPattern, recursive);

        public void CreateDirectory(string directoryPath) => _inner.CreateDirectory(directoryPath);
        public void Delete(string path) => _inner.Delete(path);

        public string CombinePath(string basePath, string relativePath)
            => _inner.CombinePath(basePath, relativePath);

        public string GetParentDirectory(string path) => _inner.GetParentDirectory(path);

        public IEnumerable<string> EnumerateDirectories(string directoryPath)
            => _inner.EnumerateDirectories(directoryPath);

        public void Rename(string sourcePath, string destinationPath)
            => _inner.Rename(sourcePath, destinationPath);

        public void DeleteDirectory(string directoryPath)
            => _inner.DeleteDirectory(directoryPath);

        public void SeedFile(string path, string content) => _inner.SeedFile(path, content);
    }
}
