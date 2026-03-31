using System.Collections.Generic;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextListSavesContractTests
{
    [Fact]
    public void ListSaves_WhenNoSaveRoot_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        Assert.Empty(ctx.ListSaves());
    }

    [Fact]
    public void ListSavesWithMetaData_ReturnsIdsAndParsesMetaMap_WhenMissingMetaMap_UsesEmptyDictionary()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();

        fs.SeedFile("root/save_002/progress.json", "{}");
        fs.SeedFile("root/save_002/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/save_002/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/save_002/level_default/session.json", "{}");
        fs.SeedFile("root/save_002/level_default/session_state_machines.json", """{"machines":[]}""");
        // meta.map intentionally missing

        fs.SeedFile("root/save_010/progress.json", "{}");
        fs.SeedFile("root/save_010/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile("root/save_010/meta.map",
            """
            # comment
            title: Chapter 2

            play_time: 03:12:55
            invalid_line_without_colon
            """);
        fs.SeedFile("root/save_010/level_default/snd_scene.json", "[]");
        fs.SeedFile("root/save_010/level_default/session.json", "{}");
        fs.SeedFile("root/save_010/level_default/session_state_machines.json", """{"machines":[]}""");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");

        var list = ctx.ListSavesWithMetaData();

        Assert.Equal(2, list.Count);
        Assert.Equal("002", list[0].SaveId);
        Assert.Empty(list[0].MetaData);
        Assert.Equal("010", list[1].SaveId);
        Assert.Equal("Chapter 2", list[1].MetaData["title"]);
        Assert.Equal("03:12:55", list[1].MetaData["play_time"]);
    }

    [Fact]
    public void ListSaves_SortsIdsOrdinal()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var fs = new TestFileSystem();
        fs.CreateDirectory("root/save_2");
        fs.CreateDirectory("root/save_10");
        fs.CreateDirectory("root/save_1");

        var ctx = new SndContext(runtime, fs, "root", "res://initial", "res://entry/entry.json");
        var ids = ctx.ListSaves();

        Assert.Equal(new List<string> { "1", "10", "2" }, ids);
    }
}
