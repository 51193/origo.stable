using Origo.Core.Runtime;
using Origo.Core.Save;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SystemBlackboardPersistenceTests
{
    [Fact]
    public void SndContext_SetContinueTarget_PersistsActiveSaveId_ToSystemJson_AndCanReload()
    {
        var fs = new TestFileSystem();
        var options = OrigoJson.CreateDefaultOptions(new TypeStringMapping());
        var path = "root/system.json";

        var persistent = new PersistentBlackboard(fs, path, options);
        var runtime = new OrigoRuntime(new TestLogger(), new TestSndSceneHost(), systemBlackboard: persistent);
        var ctx = new SndContext(runtime, fs, "root", "initial", "entry.json");

        ctx.SetContinueTarget("777");

        Assert.True(fs.Exists(path));

        var loadedBoard = new PersistentBlackboard(fs, path, options);
        loadedBoard.LoadFromDisk();
        var (found, id) = loadedBoard.TryGet<string>("origo.active_save_id");
        Assert.True(found);
        Assert.Equal("777", id);
    }
}

