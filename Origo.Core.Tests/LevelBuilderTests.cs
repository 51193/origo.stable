using System;
using System.Collections.Generic;
using Origo.Core.Abstractions;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Xunit;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;

namespace Origo.Core.Tests;

public class LevelBuilderTests
{
    private static (LevelBuilder builder, TestFileSystem fs) CreateBuilder(string levelId = "test_level")
    {
        var fs = new TestFileSystem();
        var sndWorld = TestFactory.CreateSndWorld();
        var storageService = new DefaultSaveStorageService(fs, "root");
        return (new LevelBuilder(levelId, sndWorld, storageService), fs);
    }

    // ── Constructor validation ──

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_WhenLevelIdInvalid(string? levelId)
    {
        var fs = new TestFileSystem();
        var sndWorld = TestFactory.CreateSndWorld();
        var storageService = new DefaultSaveStorageService(fs, "root");
        Assert.ThrowsAny<ArgumentException>(() => new LevelBuilder(levelId!, sndWorld, storageService));
    }

    [Fact]
    public void Constructor_Throws_WhenSndWorldNull()
    {
        var fs = new TestFileSystem();
        var storageService = new DefaultSaveStorageService(fs, "root");
        Assert.Throws<ArgumentNullException>(() => new LevelBuilder("lvl", null!, storageService));
    }

    [Fact]
    public void Constructor_Throws_WhenStorageServiceNull()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        Assert.Throws<ArgumentNullException>(() => new LevelBuilder("lvl", sndWorld, null!));
    }

    // ── AddEntity ──

    [Fact]
    public void AddEntity_AddsToSceneHost()
    {
        var (builder, _) = CreateBuilder();
        var meta = new SndMetaData { Name = "npc_01" };

        builder.AddEntity(meta);

        Assert.NotNull(builder.SceneHost.FindByName("npc_01"));
        Assert.Single(builder.SceneHost.GetEntities());
    }

    [Fact]
    public void AddEntity_ReturnsSelf_ForFluency()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.AddEntity(new SndMetaData { Name = "a" });
        Assert.Same(builder, result);
    }

    [Fact]
    public void AddEntity_Throws_WhenNameEmpty()
    {
        var (builder, _) = CreateBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddEntity(new SndMetaData { Name = "" }));
    }

    [Fact]
    public void AddEntity_Throws_WhenNull()
    {
        var (builder, _) = CreateBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddEntity(null!));
    }

    [Fact]
    public void AddEntity_Throws_WhenDuplicateName()
    {
        var (builder, _) = CreateBuilder();
        builder.AddEntity(new SndMetaData { Name = "dup" });
        Assert.Throws<InvalidOperationException>(() => builder.AddEntity(new SndMetaData { Name = "dup" }));
    }

    // ── AddEntities ──

    [Fact]
    public void AddEntities_AddsAllToSceneHost()
    {
        var (builder, _) = CreateBuilder();
        var list = new List<SndMetaData>
        {
            new() { Name = "a" },
            new() { Name = "b" },
            new() { Name = "c" }
        };
        builder.AddEntities(list);

        Assert.Equal(3, builder.SceneHost.GetEntities().Count);
    }

    // ── SetSessionData ──

    [Fact]
    public void SetSessionData_WritesToSessionBlackboard()
    {
        var (builder, _) = CreateBuilder();
        builder.SetSessionData("hp", 100);

        var (found, value) = builder.SessionBlackboard.TryGet<int>("hp");
        Assert.True(found);
        Assert.Equal(100, value);
    }

    [Fact]
    public void SetSessionData_ReturnsSelf_ForFluency()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.SetSessionData("key", "val");
        Assert.Same(builder, result);
    }

    // ── Build ──

    [Fact]
    public void Build_ReturnsValidLevelPayload()
    {
        var (builder, _) = CreateBuilder("my_level");
        builder
            .AddEntity(new SndMetaData { Name = "npc" })
            .SetSessionData("score", 42);

        var payload = builder.Build();

        Assert.Equal("my_level", payload.LevelId);
        Assert.False(string.IsNullOrWhiteSpace(payload.SndSceneJson));
        Assert.False(string.IsNullOrWhiteSpace(payload.SessionJson));
        Assert.False(string.IsNullOrWhiteSpace(payload.SessionStateMachinesJson));
    }

    [Fact]
    public void Build_EmptyLevel_ProducesValidPayload()
    {
        var (builder, _) = CreateBuilder();
        var payload = builder.Build();

        Assert.Equal("test_level", payload.LevelId);
        Assert.False(string.IsNullOrWhiteSpace(payload.SndSceneJson));
        Assert.False(string.IsNullOrWhiteSpace(payload.SessionJson));
        Assert.False(string.IsNullOrWhiteSpace(payload.SessionStateMachinesJson));
    }

    [Fact]
    public void Build_Throws_WhenCalledTwice()
    {
        var (builder, _) = CreateBuilder();
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void AddEntity_Throws_AfterBuild()
    {
        var (builder, _) = CreateBuilder();
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.AddEntity(new SndMetaData { Name = "x" }));
    }

    [Fact]
    public void SetSessionData_Throws_AfterBuild()
    {
        var (builder, _) = CreateBuilder();
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.SetSessionData("k", "v"));
    }

    // ── Commit ──

    [Fact]
    public void Commit_WritesPayloadToDisk()
    {
        var (builder, fs) = CreateBuilder("dungeon");
        builder.AddEntity(new SndMetaData { Name = "boss" });

        var payload = builder.Commit();

        Assert.Equal("dungeon", payload.LevelId);
        Assert.True(fs.Exists("root/current/level_dungeon/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_dungeon/session.json"));
        Assert.True(fs.Exists("root/current/level_dungeon/session_state_machines.json"));
    }

    [Fact]
    public void Commit_Throws_WhenCalledTwice()
    {
        var (builder, _) = CreateBuilder();
        builder.Commit();

        Assert.Throws<InvalidOperationException>(() => builder.Commit());
    }

    // ── Fluent chaining ──

    [Fact]
    public void FluentChaining_WorksEndToEnd()
    {
        var (builder, fs) = CreateBuilder("forest");

        var payload = builder
            .AddEntity(new SndMetaData { Name = "tree_01" })
            .AddEntity(new SndMetaData { Name = "tree_02" })
            .SetSessionData("weather", "rain")
            .SetSessionData("time_of_day", 18.5)
            .Commit();

        Assert.Equal("forest", payload.LevelId);
        Assert.True(fs.Exists("root/current/level_forest/snd_scene.json"));
    }

    // ── SndContext integration ──

    [Fact]
    public void SndContext_CreateLevelBuilder_ReturnsWorkingBuilder()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        var builder = ctx.CreateLevelBuilder("my_level");
        Assert.NotNull(builder);
        Assert.Equal("my_level", builder.LevelId);

        builder.AddEntity(new SndMetaData { Name = "entity_a" });
        var payload = builder.Commit();

        Assert.Equal("my_level", payload.LevelId);
        Assert.True(fs.Exists("root/current/level_my_level/snd_scene.json"));
    }
}

public class MemorySndSceneHostTests
{
    [Fact]
    public void Spawn_CreatesEntityWithCorrectName()
    {
        var host = new MemorySndSceneHost();
        var meta = new SndMetaData { Name = "npc" };

        var entity = host.Spawn(meta);

        Assert.NotNull(entity);
        Assert.Equal("npc", entity.Name);
    }

    [Fact]
    public void GetEntities_ReturnsAllSpawned()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(new SndMetaData { Name = "a" });
        host.Spawn(new SndMetaData { Name = "b" });

        Assert.Equal(2, host.GetEntities().Count);
    }

    [Fact]
    public void FindByName_ReturnsCorrectEntity()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(new SndMetaData { Name = "target" });
        host.Spawn(new SndMetaData { Name = "other" });

        var found = host.FindByName("target");
        Assert.NotNull(found);
        Assert.Equal("target", found.Name);
    }

    [Fact]
    public void FindByName_ReturnsNull_WhenNotFound()
    {
        var host = new MemorySndSceneHost();
        Assert.Null(host.FindByName("missing"));
    }

    [Fact]
    public void SerializeMetaList_ReturnsAllMeta()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(new SndMetaData { Name = "x" });
        host.Spawn(new SndMetaData { Name = "y" });

        var list = host.SerializeMetaList();
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void LoadFromMetaList_ReplacesAllEntities()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(new SndMetaData { Name = "old" });

        host.LoadFromMetaList(new[]
        {
            new SndMetaData { Name = "new1" },
            new SndMetaData { Name = "new2" }
        });

        Assert.Equal(2, host.GetEntities().Count);
        Assert.Null(host.FindByName("old"));
        Assert.NotNull(host.FindByName("new1"));
    }

    [Fact]
    public void ClearAll_RemovesAllEntities()
    {
        var host = new MemorySndSceneHost();
        host.Spawn(new SndMetaData { Name = "a" });
        host.Spawn(new SndMetaData { Name = "b" });

        host.ClearAll();

        Assert.Empty(host.GetEntities());
        Assert.Empty(host.SerializeMetaList());
    }

    [Fact]
    public void Spawn_Throws_WhenMetaDataNull()
    {
        var host = new MemorySndSceneHost();
        Assert.Throws<ArgumentNullException>(() => host.Spawn(null!));
    }

    [Fact]
    public void LoadFromMetaList_Throws_WhenNull()
    {
        var host = new MemorySndSceneHost();
        Assert.Throws<ArgumentNullException>(() => host.LoadFromMetaList(null!));
    }
}

public class MemorySndEntityTests
{
    [Fact]
    public void Name_ReturnsConstructorValue()
    {
        var entity = new MemorySndEntity("test");
        Assert.Equal("test", entity.Name);
    }

    [Fact]
    public void SetData_GetData_RoundTrips()
    {
        var entity = new MemorySndEntity("e");
        entity.SetData("hp", 100);
        Assert.Equal(100, entity.GetData<int>("hp"));
    }

    [Fact]
    public void TryGetData_ReturnsTrueAndValue_WhenExists()
    {
        var entity = new MemorySndEntity("e");
        entity.SetData("name", "hero");

        var (found, value) = entity.TryGetData<string>("name");
        Assert.True(found);
        Assert.Equal("hero", value);
    }

    [Fact]
    public void TryGetData_ReturnsFalse_WhenMissing()
    {
        var entity = new MemorySndEntity("e");
        var (found, _) = entity.TryGetData<int>("missing");
        Assert.False(found);
    }

    [Fact]
    public void GetNode_Throws_AlwaysForMemoryEntity()
    {
        var entity = new MemorySndEntity("e");
        Assert.Throws<InvalidOperationException>(() => entity.GetNode("any"));
    }

    [Fact]
    public void GetNodeNames_ReturnsEmpty()
    {
        var entity = new MemorySndEntity("e");
        Assert.Empty(entity.GetNodeNames());
    }

    [Fact]
    public void Constructor_Throws_WhenNameNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MemorySndEntity(null!));
    }

    [Fact]
    public void Subscribe_Unsubscribe_AreNoOps()
    {
        var entity = new MemorySndEntity("e");
        // Should not throw
        entity.Subscribe("key", (_, _, _) => { });
        entity.Unsubscribe("key", (_, _, _) => { });
    }

    [Fact]
    public void AddStrategy_RemoveStrategy_AreNoOps()
    {
        var entity = new MemorySndEntity("e");
        // Should not throw
        entity.AddStrategy("strat");
        entity.RemoveStrategy("strat");
    }
}
