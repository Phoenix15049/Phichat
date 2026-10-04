using Phichat.API.Hubs;

namespace Phichat.Tests;

public class PresenceTrackerTests
{
    private readonly PresenceTracker _presence = new();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public void First_connection_brings_user_online()
    {
        Assert.True(_presence.Connected(_user, "c1"));
        Assert.True(_presence.IsOnline(_user));
    }

    [Fact]
    public void Second_connection_is_not_reported_as_coming_online_again()
    {
        _presence.Connected(_user, "c1");
        Assert.False(_presence.Connected(_user, "c2"));
    }

    // Regression: a page reload opens the new connection before the old one closes.
    // The old close must not mark the user offline while the new connection is alive.
    [Fact]
    public void Late_close_of_old_connection_keeps_user_online()
    {
        _presence.Connected(_user, "old");
        _presence.Connected(_user, "new");

        Assert.False(_presence.Disconnected(_user, "old"));
        Assert.True(_presence.IsOnline(_user));
    }

    [Fact]
    public void Closing_last_connection_takes_user_offline()
    {
        _presence.Connected(_user, "c1");
        _presence.Connected(_user, "c2");
        _presence.Disconnected(_user, "c1");

        Assert.True(_presence.Disconnected(_user, "c2"));
        Assert.False(_presence.IsOnline(_user));
    }

    [Fact]
    public void Unknown_disconnect_is_ignored()
    {
        Assert.False(_presence.Disconnected(_user, "never-connected"));
    }

    [Fact]
    public void OnlineAmong_returns_only_online_users()
    {
        var other = Guid.NewGuid();
        _presence.Connected(_user, "c1");

        Assert.Equal(new[] { _user }, _presence.OnlineAmong(new[] { _user, other, _user }));
    }

    [Fact]
    public void Concurrent_connects_and_disconnects_stay_consistent()
    {
        Parallel.For(0, 1000, i => _presence.Connected(_user, $"c{i}"));
        Parallel.For(0, 999, i => _presence.Disconnected(_user, $"c{i}"));

        Assert.True(_presence.IsOnline(_user));
        Assert.True(_presence.Disconnected(_user, "c999"));
        Assert.False(_presence.IsOnline(_user));
    }
}
