using System.Threading;
using Microsoft.AspNetCore.SignalR;

namespace Motely.API.Hubs;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class SearchHub : Hub
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task JoinSearchGroup(string searchId)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"search_{searchId}");
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task LeaveSearchGroup(string searchId)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"search_{searchId}");
    }

    // Chat methods
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task SendMessage(string text, long timestamp)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // Get a simple username from connection (could be enhanced with auth)
        var username = Context.User?.Identity?.Name ?? $"User_{Context.ConnectionId[..8]}";

        // Broadcast to all clients
        await Clients.All.SendAsync("ReceiveMessage", username, text, timestamp);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public override async Task OnConnectedAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var username = Context.User?.Identity?.Name ?? $"User_{Context.ConnectionId[..8]}";
        await Clients.Others.SendAsync("UserJoined", username);
        await base.OnConnectedAsync();
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public override async Task OnDisconnectedAsync(Exception? exception)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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
