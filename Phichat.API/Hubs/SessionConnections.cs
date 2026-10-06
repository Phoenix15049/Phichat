using Phichat.Application.Interfaces;

namespace Phichat.API.Hubs;

/// <summary>
/// Open hub connections per sign-in session. A session with a live connection receives messages
/// in real time, so it does not need a push notification.
/// </summary>
public sealed class SessionConnections : IConnectedSessions
{
    private readonly Dictionary<Guid, int> _counts = new();
    private readonly object _gate = new();

    public void Connected(Guid sessionId)
    {
        lock (_gate)
        {
            _counts[sessionId] = _counts.GetValueOrDefault(sessionId) + 1;
        }
    }

    public void Disconnected(Guid sessionId)
    {
        lock (_gate)
        {
            if (!_counts.TryGetValue(sessionId, out var count)) return;
            if (count <= 1) _counts.Remove(sessionId);
            else _counts[sessionId] = count - 1;
        }
    }

    public bool IsConnected(Guid sessionId)
    {
        lock (_gate)
        {
            return _counts.ContainsKey(sessionId);
        }
    }

    /// <summary>The hub group that reaches every connection of a session.</summary>
    public static string Group(Guid sessionId) => "session:" + sessionId.ToString("N");
}
