using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Motely.API;

/// <summary>
/// WebSocket broadcaster for real-time updates to connected clients
/// </summary>
public class WebSocketBroadcaster
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly ConcurrentDictionary<string, string?> _subscriptions = new();

    /// <summary>
    /// Adds a WebSocket connection and returns its ID
    /// </summary>
    public string Add(WebSocket socket)
    {
        var id = Guid.NewGuid().ToString();
        _sockets.TryAdd(id, socket);
        return id;
    }

    /// <summary>
    /// Removes a WebSocket connection by ID
    /// </summary>
    public void Remove(string id)
    {
        if (_sockets.TryRemove(id, out var socket))
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        _subscriptions.TryRemove(id, out _);
    }

    /// <summary>
    /// Sets the subscription for a specific client
    /// </summary>
    public void SetSubscription(string clientId, string? searchId)
    {
        _subscriptions.AddOrUpdate(clientId, searchId, (_, _) => searchId);
    }

    /// <summary>
    /// Sends a message to a specific client
    /// </summary>
    public async Task SendToAsync(string clientId, string message)
    {
        if (_sockets.TryGetValue(clientId, out var socket) && socket.State == WebSocketState.Open)
        {
            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            catch
            {
                // Remove dead socket
                Remove(clientId);
            }
        }
    }

    /// <summary>
    /// Broadcasts a message to all connected clients
    /// </summary>
    public void Broadcast(string message)
    {
        var tasks = new List<Task>();
        foreach (var kvp in _sockets)
        {
            if (kvp.Value.State == WebSocketState.Open)
            {
                tasks.Add(SendToAsync(kvp.Key, message));
            }
        }
        Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Broadcasts a message to all clients subscribed to a specific search
    /// </summary>
    public void BroadcastToSearch(string searchId, object message)
    {
        var json = JsonSerializer.Serialize(message);
        var tasks = new List<Task>();
        
        foreach (var kvp in _subscriptions)
        {
            if (kvp.Value == searchId && _sockets.TryGetValue(kvp.Key, out var socket) && socket.State == WebSocketState.Open)
            {
                tasks.Add(SendToAsync(kvp.Key, json));
            }
        }
        
        Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current number of connected clients
    /// </summary>
    public int ClientCount => _sockets.Count(kvp => kvp.Value.State == WebSocketState.Open);
}
