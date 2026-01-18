using System.Threading;
using Microsoft.AspNetCore.SignalR;

namespace Motely.API.Hubs;

public class SearchHub : Hub
{
    public async Task JoinSearchGroup(string searchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"search_{searchId}");
    }

    public async Task LeaveSearchGroup(string searchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"search_{searchId}");
    }

    // Chat methods
    public async Task SendMessage(string text, long timestamp)
    {
        // Get a simple username from connection (could be enhanced with auth)
        var username = Context.User?.Identity?.Name ?? $"User_{Context.ConnectionId[..8]}";

        // Broadcast to all clients
        await Clients.All.SendAsync("ReceiveMessage", username, text, timestamp);
    }

    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.Identity?.Name ?? $"User_{Context.ConnectionId[..8]}";
        await Clients.Others.SendAsync("UserJoined", username);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var username = Context.User?.Identity?.Name ?? $"User_{Context.ConnectionId[..8]}";
            // Fire and forget - don't wait for send to complete
            _ = Clients
                .Others.SendAsync("UserLeft", username)
                .ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch { }
        // Always call base immediately, even if send fails
        await base.OnDisconnectedAsync(exception);
    }
}
