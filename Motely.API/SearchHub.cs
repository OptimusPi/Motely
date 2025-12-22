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
public class SignalRSearchBroadcaster
{
    private readonly IHubContext<SearchHub> _hubContext;

    public SignalRSearchBroadcaster(IHubContext<SearchHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public void BroadcastToSearch(string searchId, object message)
    {
        var json = JsonSerializer.Serialize(message);
        _hubContext.Clients.Group(searchId).SendAsync("Result", message).ConfigureAwait(false);
    }
}