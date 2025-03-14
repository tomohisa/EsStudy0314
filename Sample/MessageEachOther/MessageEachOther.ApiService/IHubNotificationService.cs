using Microsoft.AspNetCore.SignalR;

namespace MessageEachOther.ApiService;

/// <summary>
/// Interface for hub notification services
/// </summary>
public interface IHubNotificationService
{
    Task NotifyAllClientsAsync(string method, string name, string msg);
}
