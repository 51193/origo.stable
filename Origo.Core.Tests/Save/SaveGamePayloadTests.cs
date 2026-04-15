using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

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
