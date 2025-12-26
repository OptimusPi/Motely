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
}
