using System.Threading;
using Microsoft.AspNetCore.SignalR;

namespace Motely.API.Hubs;

/// <summary>SignalR hub for search updates and chat</summary>
public class SearchHub : Hub
{
    /// <summary>Joins a search group to receive updates</summary>
    /// <param name="searchId">Search ID to join</param>
    public async Task JoinSearchGroup(string searchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"search_{searchId}");
    }

    /// <summary>Leaves a search group</summary>
    /// <param name="searchId">Search ID to leave</param>
    public async Task LeaveSearchGroup(string searchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"search_{searchId}");
    }

    // Chat methods
    /// <summary>Sends a message to all connected clients</summary>
    /// <param name="text">Message text</param>
    /// <param name="timestamp">Message timestamp</param>
    public async Task SendMessage(string text, long timestamp)
    {
        // Get a simple username from connection (could be enhanced with auth)
        var username = Context.User?.Identity?.Name ?? $"User_{Context.ConnectionId[..8]}";

        // Broadcast to all clients
        await Clients.All.SendAsync("ReceiveMessage", username, text, timestamp);
    }

    /// <summary>Called when a client connects</summary>
    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.Identity?.Name ?? $"User_{Context.ConnectionId[..8]}";
        await Clients.Others.SendAsync("UserJoined", username);
        await base.OnConnectedAsync();
    }

    /// <summary>Called when a client disconnects</summary>
    /// <param name="exception">Disconnect exception, if any</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var username = Context.User?.Identity?.Name ?? $"User_{Context.ConnectionId[..8]}";
            await Clients.Others.SendAsync("UserLeft", username).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort notify; don't fail disconnect
        }
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }
}
