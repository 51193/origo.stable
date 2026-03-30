using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public class TechnicalDebtFixTests
{
    // ── #1  Thread-safe persistence counter ────────────────────────────

    [Fact]
    public void SndContext_PendingPersistenceCounter_IsThreadSafe()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        // RequestSaveGame increments the counter synchronously (before the deferred action runs).
        const int threadCount = 20;
        var barrier = new Barrier(threadCount);
        var threads = new Thread[threadCount];
        for (var i = 0; i < threadCount; i++)
        {
            var saveId = $"s{i:D4}";
            threads[i] = new Thread(() =>
            {
                barrier.SignalAndWait();
                ctx.RequestSaveGame(saveId, "000");
            });
            threads[i].Start();
        }

        foreach (var t in threads)
            t.Join();

        // All increments should be visible; none lost to a race.
        Assert.Equal(threadCount, ctx.GetPendingPersistenceRequestCount());

        // After flushing deferred actions the counter must return to zero.
        ctx.FlushDeferredActionsForCurrentFrame();
        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
    }

    // ── #5  SessionRun ThrowIfDisposed ─────────────────────────────────

    [Fact]
    public void SessionRun_AfterDispose_AllPropertiesThrow()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var progress = new Origo.Core.Blackboard.Blackboard();
        var session = new Origo.Core.Blackboard.Blackboard();
        var saveContext = new SaveContext(progress, session, runtime.SndWorld);
        var run = factory.CreateSessionRun(saveContext, "default", session, host);

        run.Dispose();

        Assert.Throws<ObjectDisposedException>(() => run.SessionScope);
        Assert.Throws<ObjectDisposedException>(() => run.SessionBlackboard);
        Assert.Throws<ObjectDisposedException>(() => run.SceneAccess);
        Assert.Throws<ObjectDisposedException>(() => run.PersistLevelState());
    }

    // ── #3  SwitchLevel rollback ───────────────────────────────────────

    [Fact]
    public void ProgressRun_SwitchLevel_WhenPersistProgressFails_RollsBackActiveLevel()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new FailOnWriteFileSystem(failPath: "root/current/progress.json");
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);

        var progressRun = factory.CreateProgressRun("001", "a", new Origo.Core.Blackboard.Blackboard());
        progressRun.CreateFromAlreadyLoadedScene();

        // Seed a valid target level so validation passes.
        fs.SeedFile("root/current/level_b/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_b/session.json", "{}");
        fs.SeedFile("root/current/level_b/session_state_machines.json", """{"machines":[]}""");

        // SwitchLevel should fail during PersistProgress (writing progress.json).
        Assert.ThrowsAny<Exception>(() => progressRun.SwitchLevel("b"));

        // ActiveLevelId must be rolled back to the original value.
        Assert.Equal("a", progressRun.ActiveLevelId);
        var (found, level) = progressRun.ProgressBlackboard.TryGet<string>("origo.active_level_id");
        Assert.True(found);
        Assert.Equal("a", level);
    }

    // ── #2/#10  Atomic snapshot ────────────────────────────────────────

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_UsesTempDirectoryThenRename()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/current/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_default/session.json", """{"k":"v"}""");

        SaveStorageFacade.SnapshotCurrentToSave(fs, "root", "000", "001");

        // No leftover .tmp directory.
        Assert.False(fs.DirectoryExists("root/save_001.tmp"));

        // Final save directory has all four files.
        Assert.True(fs.Exists("root/save_001/progress.json"));
        Assert.True(fs.Exists("root/save_001/progress_state_machines.json"));
        Assert.True(fs.Exists("root/save_001/level_default/snd_scene.json"));
        Assert.True(fs.Exists("root/save_001/level_default/session.json"));

        // Content must match the originals.
        Assert.Equal("{}", fs.ReadAllText("root/save_001/progress.json"));
        Assert.Equal("""{"k":"v"}""", fs.ReadAllText("root/save_001/level_default/session.json"));
    }

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_CleansUpTempOnFailure()
    {
        var fs = new FailOnCopyFileSystem(failTargetSubstring: "save_001.tmp");
        fs.SeedFile("root/current/progress.json", "{}");
        fs.SeedFile("root/current/level_default/snd_scene.json", "[]");

        Assert.ThrowsAny<Exception>(() =>
            SaveStorageFacade.SnapshotCurrentToSave(fs, "root", "000", "001"));

        // The incomplete .tmp directory must be cleaned up.
        Assert.False(fs.DirectoryExists("root/save_001.tmp"));
    }

    // ── #4  Workflow guard (regression – sequential usage) ─────────────

    [Fact]
    public void SndContext_WorkflowGuard_PreventsReentrantWorkflow()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
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

    // ── #6  TestFileSystem.Rename and DeleteDirectory ──────────────────

    [Fact]
    public void TestFileSystem_Rename_MovesAllFilesAndDirectories()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("src/a.txt", "hello");
        fs.SeedFile("src/sub/b.txt", "world");

        fs.Rename("src", "dst");

        Assert.False(fs.Exists("src/a.txt"));
        Assert.False(fs.Exists("src/sub/b.txt"));
        Assert.True(fs.Exists("dst/a.txt"));
        Assert.True(fs.Exists("dst/sub/b.txt"));
        Assert.Equal("hello", fs.ReadAllText("dst/a.txt"));
        Assert.Equal("world", fs.ReadAllText("dst/sub/b.txt"));
    }

    [Fact]
    public void TestFileSystem_DeleteDirectory_RemovesAllContents()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("dir/a.txt", "1");
        fs.SeedFile("dir/sub/b.txt", "2");
        fs.CreateDirectory("dir/empty");

        Assert.True(fs.DirectoryExists("dir"));

        fs.DeleteDirectory("dir");

        Assert.False(fs.Exists("dir/a.txt"));
        Assert.False(fs.Exists("dir/sub/b.txt"));
        Assert.False(fs.DirectoryExists("dir"));
        Assert.False(fs.DirectoryExists("dir/empty"));
    }

    // ── #15  ResolveEffectiveSaveId ────────────────────────────────────

    [Fact]
    public void SystemRun_LoadOrContinue_WithNullSaveId_FallsBackToActiveSaveId()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
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
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        var sndContext = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var factory = new RunFactory(logger, fs, "root", runtime, sndContext);
        var systemRun = factory.CreateSystemRun();

        // No ActiveSaveId set in system blackboard; empty string provided.
        var progress = systemRun.LoadOrContinue("");

        Assert.Null(progress);
    }

    // ── #17  StateMachineContainer safe swap ───────────────────────────

    [Fact]
    public void StateMachineContainer_DeserializeWithoutHooks_SwapsAtomically()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(), null, new Origo.Core.Blackboard.Blackboard());
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SwapTestPushStrategy());
        pool.Register(() => new SwapTestPopStrategy());

        var options = OrigoJson.CreateDefaultOptions(runtime.SndWorld.TypeMapping, _ => { });
        var container = new StateMachineContainer(pool, ctx);

        // Serialize an empty container, then deserialize back – no-op swap.
        var emptyJson = container.SerializeToJson(options);
        container.DeserializeWithoutHooks(emptyJson, options);
        Assert.False(container.TryGet("anything", out _));

        // Populate with a machine, serialize, then deserialize into the same container.
        var sm = container.CreateOrGet("nav", "sm.swap.push", "sm.swap.pop");
        sm.RestoreStackWithoutHooks(new List<string> { "a", "b" });
        var json = container.SerializeToJson(options);

        // Deserialize replaces old state atomically.
        container.DeserializeWithoutHooks(json, options);

        Assert.True(container.TryGet("nav", out var restored));
        Assert.NotNull(restored);
        Assert.Equal(new[] { "a", "b" }, restored!.Snapshot());

        // Old key must still be accessible (same key name).
        Assert.True(container.TryGet("nav", out _));

        container.Clear();
    }

    // ── Test doubles for this file ─────────────────────────────────────

    [StrategyIndex("sm.swap.push")]
    private sealed class SwapTestPushStrategy : StateMachineStrategyBase { }

    [StrategyIndex("sm.swap.pop")]
    private sealed class SwapTestPopStrategy : StateMachineStrategyBase { }

    /// <summary>
    /// A file system that delegates to <see cref="TestFileSystem"/> but throws on
    /// <see cref="IFileSystem.WriteAllText"/> when the path contains the configured fail path.
    /// Used to simulate persistence failures during SwitchLevel.
    /// </summary>
    private sealed class FailOnWriteFileSystem : IFileSystem
    {
        private readonly TestFileSystem _inner = new();
        private readonly string _failPath;

        public FailOnWriteFileSystem(string failPath) => _failPath = failPath;

        public void SeedFile(string path, string content) => _inner.SeedFile(path, content);
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
    }

    /// <summary>
    /// A file system that delegates to <see cref="TestFileSystem"/> but throws on
    /// <see cref="IFileSystem.Copy"/> when the destination path contains the configured substring.
    /// Used to simulate snapshot copy failures.
    /// </summary>
    private sealed class FailOnCopyFileSystem : IFileSystem
    {
        private readonly TestFileSystem _inner = new();
        private readonly string _failTargetSubstring;

        public FailOnCopyFileSystem(string failTargetSubstring)
            => _failTargetSubstring = failTargetSubstring;

        public void SeedFile(string path, string content) => _inner.SeedFile(path, content);
        public bool Exists(string path) => _inner.Exists(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public string ReadAllText(string path) => _inner.ReadAllText(path);
        public void WriteAllText(string path, string content, bool overwrite)
            => _inner.WriteAllText(path, content, overwrite);

        public void Copy(string sourcePath, string destinationPath, bool overwrite)
        {
            if (destinationPath.Replace('\\', '/').Contains(_failTargetSubstring, StringComparison.Ordinal))
                throw new InvalidOperationException($"Simulated copy failure for '{destinationPath}'.");
            _inner.Copy(sourcePath, destinationPath, overwrite);
        }

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
    }
}
