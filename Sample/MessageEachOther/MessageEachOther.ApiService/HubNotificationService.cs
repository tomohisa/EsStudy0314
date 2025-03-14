using Microsoft.AspNetCore.SignalR;

namespace MessageEachOther.ApiService;

/// <summary>
/// Implementation of hub notification service for a specific hub type
/// </summary>
/// <typeparam name="THub">The hub type</typeparam>
public class HubNotificationService<THub> : IHubNotificationService where THub : Hub
{
    private readonly IHubContext<THub> _hubContext;

    public HubNotificationService(IHubContext<THub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyAllClientsAsync(string method, string name, string msg)
    {
        await _hubContext.Clients.All.SendAsync(method, name, msg);
    }
}
