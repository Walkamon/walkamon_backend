using BLL.Service;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class PvpPresenceTrackerTests
{
    [Fact]
    public void MultipleConnections_StayOnlineUntilTheLastConnectionLeaves()
    {
        var tracker = new PvpPresenceTracker();
        var userId = Guid.NewGuid();

        Assert.True(tracker.RegisterConnection(userId, "phone"));
        Assert.False(tracker.RegisterConnection(userId, "tablet"));
        Assert.True(tracker.IsOnline(userId));

        Assert.False(tracker.UnregisterConnection(userId, "phone"));
        Assert.True(tracker.IsOnline(userId));

        Assert.True(tracker.UnregisterConnection(userId, "tablet"));
        Assert.False(tracker.IsOnline(userId));
        Assert.False(tracker.UnregisterConnection(userId, "unknown"));
    }
}
