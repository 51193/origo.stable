using System;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class DataObserverManagerTests
{
    [Fact]
    public void NotifyObservers_CallbackUnsubscribes_DoesNotThrow()
    {
        var mgr = new DataObserverManager();
        Action<object?, object?> cb = null!;
        cb = (_, _) => mgr.Unsubscribe("key", cb);
        mgr.Subscribe("key", cb);
        mgr.NotifyObservers("key", 1, 2);
    }
}
