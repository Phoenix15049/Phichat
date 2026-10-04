namespace Phichat.API.Hubs;

/// <summary>
/// Tracks every open hub connection per user. A user is online while at least one
/// connection is open, so a second tab, a page reload or a reconnect whose old
/// connection closes late never makes a connected user look offline.
/// </summary>
public sealed class PresenceTracker
{
    private readonly Dictionary<Guid, HashSet<string>> _connections = new();
    private readonly object _gate = new();

    /// <returns>True when this is the user's first open connection (they just came online).</returns>
    public bool Connected(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var set))
            {
                set = new HashSet<string>();
                _connections[userId] = set;
            }

            set.Add(connectionId);
            return set.Count == 1;
        }
    }

    /// <returns>True when the user's last connection closed (they just went offline).</returns>
    public bool Disconnected(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var set))
                return false;

            set.Remove(connectionId);
            if (set.Count > 0)
                return false;

            _connections.Remove(userId);
            return true;
        }
    }

    public bool IsOnline(Guid userId)
    {
        lock (_gate)
        {
            return _connections.ContainsKey(userId);
        }
    }

    /// <summary>The subset of <paramref name="userIds"/> that is currently online.</summary>
    public List<Guid> OnlineAmong(IEnumerable<Guid> userIds)
    {
        lock (_gate)
        {
            return userIds.Where(_connections.ContainsKey).Distinct().ToList();
        }
    }
}
