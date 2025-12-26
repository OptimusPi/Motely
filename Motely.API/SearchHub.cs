using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace Motely.API;

/// <summary>
/// SignalR Hub for real-time search updates
/// </summary>
public class SearchHub : Hub
{
    public async Task JoinSearchGroup(string searchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, searchId);
    }

    public async Task LeaveSearchGroup(string searchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, searchId);
    }
}

/// <summary>
/// SignalR broadcaster adapter for SearchManager
/// </summary>
public class SignalRSearchBroadcaster : ISearchBroadcaster
{
    private readonly IHubContext<SearchHub> _hubContext;

    public SignalRSearchBroadcaster(IHubContext<SearchHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public void Broadcast(string message)
    {
        _hubContext.Clients.All.SendAsync("Broadcast", message).ConfigureAwait(false);
    }

    public void BroadcastToSearch(string searchId, string json)
    {
        if (string.IsNullOrWhiteSpace(searchId) || string.IsNullOrWhiteSpace(json))
            return;

        _hubContext.Clients.Group(searchId).SendAsync("Result", json).ConfigureAwait(false);
    }

    public void BroadcastToSearch(string searchId, object message)
    {
        if (string.IsNullOrWhiteSpace(searchId))
            return;

        _hubContext.Clients.Group(searchId).SendAsync("Result", message).ConfigureAwait(false);
    }
}