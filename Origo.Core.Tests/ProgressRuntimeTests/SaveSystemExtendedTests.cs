using System;
using System.Collections.Generic;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Serialization;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class SavePathLayoutTests
{
    [Fact]
    public void SavePathLayout_GetCurrentDirectory_ReturnsCurrent() =>
        Assert.Equal("current", SavePathLayout.GetCurrentDirectory());

    [Fact]
    public void SavePathLayout_CurrentDirectoryName_Constant() =>
        Assert.Equal("current", SavePathLayout.CurrentDirectoryName);

    [Theory]
    [InlineData("001", "save_001")]
    [InlineData("abc", "save_abc")]
    [InlineData("my-save", "save_my-save")]
    public void SavePathLayout_GetSaveDirectory_FormatsCorrectly(string saveId, string expected) =>
        Assert.Equal(expected, SavePathLayout.GetSaveDirectory(saveId));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SavePathLayout_GetSaveDirectory_ThrowsOnInvalidId(string? saveId) =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetSaveDirectory(saveId!));

    [Fact]
    public void SavePathLayout_GetProgressFile_CombinesCorrectly() =>
        Assert.Equal("mybase/progress.json", SavePathLayout.GetProgressFile("mybase"));

    [Fact]
    public void SavePathLayout_GetProgressFile_ThrowsOnEmpty() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetProgressFile(""));

    [Fact]
    public void SavePathLayout_GetProgressStateMachinesFile_CombinesCorrectly() =>
        Assert.Equal("base/progress_state_machines.json", SavePathLayout.GetProgressStateMachinesFile("base"));

    [Fact]
    public void SavePathLayout_GetProgressStateMachinesFile_ThrowsOnWhitespace() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetProgressStateMachinesFile("  "));

    [Fact]
    public void SavePathLayout_GetCustomMetaFile_CombinesCorrectly() =>
        Assert.Equal("base/meta.map", SavePathLayout.GetCustomMetaFile("base"));

    [Fact]
    public void SavePathLayout_GetCustomMetaFile_ThrowsOnNull() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetCustomMetaFile(null!));

    [Fact]
    public void SavePathLayout_GetLevelDirectory_CombinesCorrectly() =>
        Assert.Equal("base/level_town", SavePathLayout.GetLevelDirectory("base", "town"));

    [Theory]
    [InlineData("", "level1")]
    [InlineData("base", "")]
    [InlineData("", "")]
    public void SavePathLayout_GetLevelDirectory_ThrowsOnInvalidArgs(string baseDir, string levelId) =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelDirectory(baseDir, levelId));

    [Fact]
    public void SavePathLayout_GetLevelSndSceneFile_CombinesCorrectly() => Assert.Equal("level_dir/snd_scene.json",
        SavePathLayout.GetLevelSndSceneFile("level_dir"));

    [Fact]
    public void SavePathLayout_GetLevelSndSceneFile_ThrowsOnEmpty() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelSndSceneFile(""));

    [Fact]
    public void SavePathLayout_GetLevelSessionFile_CombinesCorrectly() => Assert.Equal("level_dir/session.json",
        SavePathLayout.GetLevelSessionFile("level_dir"));

    [Fact]
    public void SavePathLayout_GetLevelSessionFile_ThrowsOnWhitespace() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelSessionFile("   "));

    [Fact]
    public void SavePathLayout_GetLevelSessionStateMachinesFile_CombinesCorrectly()
    {
        Assert.Equal("level_dir/session_state_machines.json",
            SavePathLayout.GetLevelSessionStateMachinesFile("level_dir"));
    }

    [Fact]
    public void SavePathLayout_GetLevelSessionStateMachinesFile_ThrowsOnNull() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelSessionStateMachinesFile(null!));

    [Fact]
    public void SavePathLayout_GetWriteInProgressMarker_CombinesCorrectly() => Assert.Equal("base/.write_in_progress",
        SavePathLayout.GetWriteInProgressMarker("base"));

    [Fact]
    public void SavePathLayout_GetWriteInProgressMarker_ThrowsOnEmpty() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetWriteInProgressMarker(""));

    [Fact]
    public void SavePathLayout_WriteInProgressMarkerName_Constant() =>
        Assert.Equal(".write_in_progress", SavePathLayout.WriteInProgressMarkerName);
}

// ── SavePathResolver ───────────────────────────────────────────────────

public class SavePathResolverTests
{
    [Fact]
    public void SavePathResolver_EnsureParentDirectory_CreatesParent()
    {
        var fs = new TestFileSystem();
        SavePathResolver.EnsureParentDirectory(fs, "root/sub/file.txt");
        Assert.True(fs.DirectoryExists("root/sub"));
    }

    [Fact]
    public void SavePathResolver_EnsureParentDirectory_NoOpForRootFile()
    {
        var fs = new TestFileSystem();
        // File at root has empty parent; should not throw
        SavePathResolver.EnsureParentDirectory(fs, "file.txt");
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_ExtractsRelative()
    {
        var result = SavePathResolver.GetRelativePath("root/saves", "root/saves/file.json");
        Assert.Equal("file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_NestedPath()
    {
        var result = SavePathResolver.GetRelativePath("root", "root/sub/file.json");
        Assert.Equal("sub/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_ExactMatch_ReturnsEmpty()
    {
        var result = SavePathResolver.GetRelativePath("root/saves", "root/saves");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_NoMatch_ReturnsFullPath()
    {
        var result = SavePathResolver.GetRelativePath("root/a", "root/b/file.json");
        Assert.Equal("root/b/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_EmptyBase_ReturnsFullPath()
    {
        var result = SavePathResolver.GetRelativePath("", "root/file.json");
        Assert.Equal("root/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_ReturnsLastSegment() =>
        Assert.Equal("child", SavePathResolver.GetLeafDirectoryName("root/parent/child"));

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_SingleSegment() =>
        Assert.Equal("single", SavePathResolver.GetLeafDirectoryName("single"));

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_TrailingSlash() =>
        Assert.Equal("child", SavePathResolver.GetLeafDirectoryName("root/child/"));

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SavePathResolver.GetLeafDirectoryName(""));
        Assert.Equal(string.Empty, SavePathResolver.GetLeafDirectoryName("  "));
    }

    [Fact]
    public void SavePathResolver_RejectPathTraversal_ThrowsOnDotDot()
    {
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal("../evil"));
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal("some/../evil"));
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal(".."));
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal("path/.."));
    }

    [Fact]
    public void SavePathResolver_RejectPathTraversal_AllowsSafePaths()
    {
        // Should not throw
        SavePathResolver.RejectPathTraversal("safe/path");
        SavePathResolver.RejectPathTraversal("file.json");
        SavePathResolver.RejectPathTraversal("");
    }
}

// ── SaveMetaMapCodec ───────────────────────────────────────────────────

public class SaveMetaMapCodecExtendedTests
{
    [Fact]
    public void SaveMetaMapCodec_Parse_BasicContent()
    {
        var logger = new TestLogger();
        var result = SaveMetaMapCodec.Parse("key1: value1\nkey2: value2", logger);
        Assert.Equal(2, result.Count);
        Assert.Equal("value1", result["key1"]);
    }

    [Fact]
    public void SaveMetaMapCodec_Parse_NullContent_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var result = SaveMetaMapCodec.Parse(null, logger);
        Assert.Empty(result);
    }

    [Fact]
    public void SaveMetaMapCodec_Serialize_SortsByKey()
    {
        var map = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };
        var text = SaveMetaMapCodec.Serialize(map);
        Assert.StartsWith("a: 1", text);
        Assert.Contains("b: 2", text);
    }

    [Fact]
    public void SaveMetaMapCodec_Serialize_NullMap_ReturnsEmpty() =>
        Assert.Equal(string.Empty, SaveMetaMapCodec.Serialize(null));

    [Fact]
    public void SaveMetaMapCodec_Serialize_EmptyMap_ReturnsEmpty() => Assert.Equal(string.Empty,
        SaveMetaMapCodec.Serialize(new Dictionary<string, string>()));

    [Fact]
    public void SaveMetaMapCodec_RoundTrip()
    {
        var logger = new TestLogger();
        var original = new Dictionary<string, string> { ["name"] = "Test", ["score"] = "100" };
        var serialized = SaveMetaMapCodec.Serialize(original);
        var parsed = SaveMetaMapCodec.Parse(serialized, logger);
        Assert.Equal("Test", parsed["name"]);
        Assert.Equal("100", parsed["score"]);
    }
}

// ── DelegateSaveMetaContributor ────────────────────────────────────────

public class DelegateSaveMetaContributorTests
{
    [Fact]
    public void DelegateSaveMetaContributor_Contribute_InvokesDelegate()
    {
        var invoked = false;
        var contributor = new DelegateSaveMetaContributor((ctx, meta) =>
        {
            invoked = true;
            meta["custom_key"] = "custom_value";
        });

        var bb = new Blackboard.Blackboard();
        var host = new TestSndSceneHost();
        var context = new SaveMetaBuildContext("save1", "level1", bb, bb, host);
        var dict = new Dictionary<string, string>();

        contributor.Contribute(context, dict);
        Assert.True(invoked);
        Assert.Equal("custom_value", dict["custom_key"]);
    }

    [Fact]
    public void DelegateSaveMetaContributor_Constructor_ThrowsOnNull() =>
        Assert.Throws<ArgumentNullException>(() => new DelegateSaveMetaContributor(null!));
}

// ── SaveContext ─────────────────────────────────────────────────────────

public class SaveContextTests
{
    private static SndWorld CreateWorld() => TestFactory.CreateSndWorld();

    [Fact]
    public void SaveContext_SerializeProgress_And_DeserializeProgress_RoundTrip()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        progress.Set("stage", 3);

        var ctx = new SaveContext(progress, session, world);
        var json = ctx.SerializeProgress();

        var progress2 = new Blackboard.Blackboard();
        var ctx2 = new SaveContext(progress2, new Blackboard.Blackboard(), world);
        ctx2.DeserializeProgress(json);

        Assert.Equal(3, progress2.TryGet<int>("stage").value);
    }

    [Fact]
    public void SaveContext_SerializeSession_And_DeserializeSession_RoundTrip()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        session.Set("hp", 100);

        var ctx = new SaveContext(progress, session, world);
        var json = ctx.SerializeSession();

        var session2 = new Blackboard.Blackboard();
        var ctx2 = new SaveContext(new Blackboard.Blackboard(), session2, world);
        ctx2.DeserializeSession(json);

        Assert.Equal(100, session2.TryGet<int>("hp").value);
    }

    [Fact]
    public void SaveContext_SerializeSndScene_ReturnsJson()
    {
        var world = CreateWorld();
        var ctx = new SaveContext(new Blackboard.Blackboard(), new Blackboard.Blackboard(), world);
        var host = new TestSndSceneHost();

        var json = ctx.SerializeSndScene(host);
        Assert.NotNull(json);
    }

    [Fact]
    public void SaveContext_DeserializeSndScene_ClearsAndLoads()
    {
        var world = CreateWorld();
        var ctx = new SaveContext(new Blackboard.Blackboard(), new Blackboard.Blackboard(), world);
        var host = new TestSndSceneHost();
        host.Spawn(new SndMetaData { Name = "old" });

        ctx.DeserializeSndScene(host, "[]");
        Assert.Equal(1, host.ClearAllCount);
    }

    [Fact]
    public void SaveContext_SaveGame_CreatesSaveGamePayload()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        progress.Set("gold", 500);
        progress.Set(WellKnownKeys.ActiveLevelId, "level1");

        var ctx = new SaveContext(progress, session, world);
        var host = new TestSndSceneHost();

        var payload = ctx.SaveGame(host, "slot1", "level1", null, "{}", "{}");

        Assert.Equal("slot1", payload.SaveId);
        Assert.Equal("level1", payload.ActiveLevelId);
        Assert.NotNull(payload.ProgressJson);
        Assert.Contains("level1", payload.Levels.Keys);
    }

    [Fact]
    public void SaveContext_SaveGame_WithCustomMeta()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        progress.Set(WellKnownKeys.ActiveLevelId, "level1");
        var ctx = new SaveContext(progress, new Blackboard.Blackboard(), world);
        var host = new TestSndSceneHost();
        var meta = new Dictionary<string, string> { ["display"] = "Save 1" };

        var payload = ctx.SaveGame(host, "slot1", "level1", meta, "{}", "{}");

        Assert.NotNull(payload.CustomMeta);
        Assert.Equal("Save 1", payload.CustomMeta!["display"]);
    }

    [Fact]
    public void SaveContext_Constructor_ThrowsOnNullArgs()
    {
        var world = CreateWorld();
        var bb = new Blackboard.Blackboard();
        Assert.Throws<ArgumentNullException>(() => new SaveContext(null!, bb, world));
        Assert.Throws<ArgumentNullException>(() => new SaveContext(bb, null!, world));
        Assert.Throws<ArgumentNullException>(() => new SaveContext(bb, bb, null!));
    }

    [Fact]
    public void SaveContext_Properties_ExposeBlackboards()
    {
        var world = CreateWorld();
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        var ctx = new SaveContext(progress, session, world);

        Assert.Same(progress, ctx.Progress);
        Assert.Same(session, ctx.Session);
        Assert.Same(world, ctx.SndWorld);
    }
}

// ── SaveMetaBuildContext ────────────────────────────────────────────────

public class SaveMetaBuildContextTests
{
    [Fact]
    public void SaveMetaBuildContext_StoresAllProperties()
    {
        var progress = new Blackboard.Blackboard();
        var session = new Blackboard.Blackboard();
        var host = new TestSndSceneHost();
        var ctx = new SaveMetaBuildContext("s1", "lvl1", progress, session, host);

        Assert.Equal("s1", ctx.SaveId);
        Assert.Equal("lvl1", ctx.CurrentLevelId);
        Assert.Same(progress, ctx.Progress);
        Assert.Same(session, ctx.Session);
        Assert.Same(host, ctx.SceneAccess);
    }

    [Fact]
    public void SaveMetaBuildContext_ThrowsOnNullArgs()
    {
        var bb = new Blackboard.Blackboard();
        var host = new TestSndSceneHost();

        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext(null!, "l", bb, bb, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", null!, bb, bb, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", "l", null!, bb, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", "l", bb, null!, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", "l", bb, bb, null!));
    }
}

// ── SaveGamePayload ────────────────────────────────────────────────────

public class SaveGamePayloadTests
{
    [Fact]
    public void SaveGamePayload_CurrentFormatVersion_IsOne() => Assert.Equal(1, SaveGamePayload.CurrentFormatVersion);

    [Fact]
    public void SaveGamePayload_DefaultValues()
    {
        var payload = new SaveGamePayload();
        Assert.Equal(SaveGamePayload.CurrentFormatVersion, payload.FormatVersion);
        Assert.Equal(string.Empty, payload.SaveId);
        Assert.Equal(string.Empty, payload.ActiveLevelId);
        Assert.Equal(string.Empty, payload.ProgressJson);
        Assert.NotNull(payload.Levels);
    }

    [Fact]
    public void LevelPayload_DefaultValues()
    {
        var lp = new LevelPayload();
        Assert.Equal(string.Empty, lp.LevelId);
        Assert.Equal(string.Empty, lp.SndSceneJson);
        Assert.Equal(string.Empty, lp.SessionJson);
        Assert.Equal(string.Empty, lp.SessionStateMachinesJson);
    }
}
