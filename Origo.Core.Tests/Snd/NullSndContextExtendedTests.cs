using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class NullSndContextExtendedTests
{
    [Fact]
    public void ListSaves_ReturnsEmpty() => Assert.Empty(NullSndContext.Instance.ListSaves());

    [Fact]
    public void RequestSaveGameAuto_WithNull_ReturnsEmpty() =>
        Assert.Equal(string.Empty, NullSndContext.Instance.RequestSaveGameAuto());

    [Fact]
    public void RequestSaveGameAuto_WithValue_ReturnsSameValue() =>
        Assert.Equal("my_save", NullSndContext.Instance.RequestSaveGameAuto("my_save"));

    [Fact]
    public void HasContinueData_ReturnsFalse() => Assert.False(NullSndContext.Instance.HasContinueData());

    [Fact]
    public void RequestContinueGame_ReturnsFalse() => Assert.False(NullSndContext.Instance.RequestContinueGame());

    [Fact]
    public void VoidOperations_DoNotChangeObservableState()
    {
        var ex = Record.Exception(() =>
        {
            NullSndContext.Instance.RequestLoadGame("any");
            NullSndContext.Instance.RequestSaveGame("any");
            NullSndContext.Instance.SetContinueTarget("any");
            NullSndContext.Instance.RequestSwitchForegroundLevel("level");
            NullSndContext.Instance.RequestLoadInitialSave();
            NullSndContext.Instance.RequestLoadMainMenuEntrySave();
        });

        Assert.Null(ex);
        Assert.False(NullSndContext.Instance.HasContinueData());
        Assert.Empty(NullSndContext.Instance.ListSaves());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. SessionSndContext — pass-through delegation
// ─────────────────────────────────────────────────────────────────────────────
