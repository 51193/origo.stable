using System.Linq;
using System.Text.Json;
using Origo.Core.Save;
using Origo.Core.Serialization;
using Xunit;

namespace Origo.Core.Tests;

public class PersistentBlackboardTests
{
    [Fact]
    public void PersistentBlackboard_SetAndLoadFromDisk_Works()
    {
        var fs = new TestFileSystem();
        var options = OrigoJson.CreateDefaultOptions(new TypeStringMapping());
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(fs, path, options, new Origo.Core.Blackboard.Blackboard());

        board.Set("n", 7);
        var loaded = new PersistentBlackboard(fs, path, options, new Origo.Core.Blackboard.Blackboard());
        loaded.LoadFromDisk();
        var (found, n) = loaded.TryGet<int>("n");

        Assert.True(found);
        Assert.Equal(7, n);
    }

    [Fact]
    public void PersistentBlackboard_Clear_PersistsEmptyData()
    {
        var fs = new TestFileSystem();
        var options = OrigoJson.CreateDefaultOptions(new TypeStringMapping());
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(fs, path, options, new Origo.Core.Blackboard.Blackboard());
        board.Set("x", 1);
        board.Clear();

        var json = fs.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Empty(doc.RootElement.EnumerateObject());
    }
}
