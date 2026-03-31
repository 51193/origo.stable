using Origo.Core.DataSource;
using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

public class PersistentBlackboardTests
{
    [Fact]
    public void PersistentBlackboard_SetAndLoadFromDisk_Works()
    {
        var fs = new TestFileSystem();
        var codec = TestFactory.CreateJsonCodec();
        var registry = TestFactory.CreateRegistry();
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(fs, path, codec, registry, new Blackboard.Blackboard());

        board.Set("n", 7);
        var loaded = new PersistentBlackboard(fs, path, codec, registry, new Blackboard.Blackboard());
        loaded.LoadFromDisk();
        var (found, n) = loaded.TryGet<int>("n");

        Assert.True(found);
        Assert.Equal(7, n);
    }

    [Fact]
    public void PersistentBlackboard_Clear_PersistsEmptyData()
    {
        var fs = new TestFileSystem();
        var codec = TestFactory.CreateJsonCodec();
        var registry = TestFactory.CreateRegistry();
        var path = "user://origo/system.json";
        var board = new PersistentBlackboard(fs, path, codec, registry, new Blackboard.Blackboard());
        board.Set("x", 1);
        board.Clear();

        var json = fs.ReadAllText(path);
        var node = codec.Decode(json);
        Assert.Equal(DataSourceNodeKind.Object, node.Kind);
        Assert.Empty(node.Keys);
    }
}
