using System;
using System.Collections.Generic;
using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

public class SaveStorageAndPayloadTests
{
    [Fact]
    public void SaveStorageFacade_WriteAndReadCurrent_RoundTrip()
    {
        var fs = new TestFileSystem();
        var progressSm = """{"machines":[]}""";
        var sessionSm = """{"machines":[]}""";
        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressJson = """{"k":{"type":"Int32","data":1}}""",
            ProgressStateMachinesJson = progressSm,
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneJson = "[]",
                    SessionJson = """{"s":{"type":"String","data":"ok"}}""",
                    SessionStateMachinesJson = sessionSm
                }
            }
        };

        SaveStorageFacade.WriteSavePayloadToCurrent(fs, "user://origo_saves", payload);
        var loaded = SaveStorageFacade.ReadSavePayloadFromCurrent(fs, "user://origo_saves", "001", "default");

        Assert.Equal("001", loaded.SaveId);
        Assert.Equal("default", loaded.ActiveLevelId);
        Assert.Contains("\"k\"", loaded.ProgressJson);
        Assert.Equal("[]", loaded.Levels["default"].SndSceneJson);
        Assert.Equal(progressSm, loaded.ProgressStateMachinesJson);
        Assert.Equal(sessionSm, loaded.Levels["default"].SessionStateMachinesJson);
    }

    [Fact]
    public void SaveStorageFacade_ReadCurrent_MissingProgressStateMachines_Throws()
    {
        var fs = new TestFileSystem();
        var root = "root";
        var currentRel = SavePathLayout.GetCurrentDirectory();
        var progressAbs = fs.CombinePath(root, SavePathLayout.GetProgressFile(currentRel));
        var sndSceneAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSndSceneFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));
        var sessionAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSessionFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));
        var sessionSmAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSessionStateMachinesFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));

        fs.SeedFile(progressAbs, "{}");
        fs.SeedFile(sndSceneAbs, "[]");
        fs.SeedFile(sessionAbs, "{}");
        fs.SeedFile(sessionSmAbs, """{"machines":[]}""");

        Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.ReadSavePayloadFromCurrent(fs, root, "001", "default"));
    }

    [Fact]
    public void SaveStorageFacade_ReadCurrent_MissingSessionStateMachines_Throws()
    {
        var fs = new TestFileSystem();
        var root = "root";
        var currentRel = SavePathLayout.GetCurrentDirectory();
        var progressAbs = fs.CombinePath(root, SavePathLayout.GetProgressFile(currentRel));
        var progressSmAbs = fs.CombinePath(root, SavePathLayout.GetProgressStateMachinesFile(currentRel));
        var sndSceneAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSndSceneFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));
        var sessionAbs = fs.CombinePath(root,
            SavePathLayout.GetLevelSessionFile(SavePathLayout.GetLevelDirectory(currentRel, "default")));

        fs.SeedFile(progressAbs, "{}");
        fs.SeedFile(progressSmAbs, """{"machines":[]}""");
        fs.SeedFile(sndSceneAbs, "[]");
        fs.SeedFile(sessionAbs, "{}");

        Assert.Throws<InvalidOperationException>(() =>
            SaveStorageFacade.ReadSavePayloadFromCurrent(fs, root, "001", "default"));
    }

    [Fact]
    public void SaveStorageFacade_SnapshotCurrentToSave_AndEnumerateSaveIds_Works()
    {
        var fs = new TestFileSystem();
        var payload = new SaveGamePayload
        {
            SaveId = "001",
            ActiveLevelId = "default",
            ProgressJson = "{}",
            ProgressStateMachinesJson = """{"machines":[]}""",
            Levels = new Dictionary<string, LevelPayload>
            {
                ["default"] = new()
                {
                    LevelId = "default",
                    SndSceneJson = "[]",
                    SessionJson = "{}",
                    SessionStateMachinesJson = """{"machines":[]}"""
                }
            }
        };
        SaveStorageFacade.WriteSavePayloadToCurrent(fs, "root", payload);

        SaveStorageFacade.SnapshotCurrentToSave(fs, "root", "000", "001");
        var ids = SaveStorageFacade.EnumerateSaveIds(fs, "root");

        Assert.Contains("001", ids);
    }
}
