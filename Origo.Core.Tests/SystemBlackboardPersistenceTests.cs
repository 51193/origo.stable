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
}
