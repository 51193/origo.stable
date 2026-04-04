using System;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

public class EmptySessionManagerTests
{
    [Fact]
    public void EmptySessionManager_CreateBackgroundSession_Throws()
    {
        var m = EmptySessionManager.Instance;
        var ex = Assert.Throws<InvalidOperationException>(() =>
            m.CreateBackgroundSession("k", "level"));
        Assert.Contains("ProgressRun", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptySessionManager_CreateBackgroundSessionFromPayload_Throws()
    {
        var m = EmptySessionManager.Instance;
        var payload = new LevelPayload
        {
            LevelId = "l",
            SndSceneJson = "[]",
            SessionJson = "{}",
            SessionStateMachinesJson = """{"machines":[]}"""
        };
        Assert.Throws<InvalidOperationException>(() =>
            m.CreateBackgroundSessionFromPayload("k", "level", payload));
    }

    [Fact]
    public void EmptySessionManager_QueryAndNoOps()
    {
        var m = EmptySessionManager.Instance;
        Assert.Null(m.ForegroundSession);
        Assert.Empty(m.Keys);
        Assert.Null(m.TryGet("any"));
        Assert.False(m.Contains("any"));
        m.DestroySession("any");
        m.ProcessBackgroundSessions(0.016);
    }
}
