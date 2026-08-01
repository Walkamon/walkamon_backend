using BLL.Interfaces;

namespace BLL.Service;

public sealed class PvpPresenceTracker : IPvpPresenceTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, HashSet<string>> _connections = [];

    public bool RegisterConnection(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var connections))
            {
                connections = new HashSet<string>(StringComparer.Ordinal);
                _connections[userId] = connections;
            }

            var wasOffline = connections.Count == 0;
            connections.Add(connectionId);
            return wasOffline;
        }
    }

    public bool UnregisterConnection(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var connections) ||
                !connections.Remove(connectionId))
            {
                return false;
            }

            if (connections.Count > 0)
            {
                return false;
            }

            _connections.Remove(userId);
            return true;
        }
    }

    public bool IsOnline(Guid userId)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(userId, out var connections) &&
                   connections.Count > 0;
        }
    }
}
