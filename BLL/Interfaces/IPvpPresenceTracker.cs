namespace BLL.Interfaces;

public interface IPvpPresenceTracker
{
    bool RegisterConnection(Guid userId, string connectionId);
    bool UnregisterConnection(Guid userId, string connectionId);
    bool IsOnline(Guid userId);
}
