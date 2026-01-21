namespace Motely.API;

/// <summary>
/// Interface for broadcasting search updates to clients
/// </summary>
public interface ISearchBroadcaster
{
    /// <summary>
    /// Broadcasts a message to all connected clients
    /// </summary>
    Task BroadcastAsync(string message);

    /// <summary>
    /// Broadcasts a message to clients subscribed to a specific search
    /// </summary>
    Task BroadcastToSearchAsync(string searchId, string json);

    /// <summary>
    /// Broadcasts an object to clients subscribed to a specific search (serializes automatically)
    /// </summary>
    Task BroadcastToSearchAsync(string searchId, object message);
}
