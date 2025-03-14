using Microsoft.AspNetCore.SignalR;

namespace EsCQRSQuestions.ApiService;

/// <summary>
/// Implementation of hub notification service for the QuestionHub
/// </summary>
public class HubNotificationService : IHubNotificationService
{
    private readonly IHubContext<QuestionHub> _hubContext;

    public HubNotificationService(IHubContext<QuestionHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyAllClientsAsync(string method, object data)
    {
        await _hubContext.Clients.All.SendAsync(method, data);
    }

    public async Task NotifyAdminsAsync(string method, object data)
    {
        await _hubContext.Clients.Group("Admins").SendAsync(method, data);
    }

    public async Task NotifyParticipantsAsync(string method, object data)
    {
        await _hubContext.Clients.Group("Participants").SendAsync(method, data);
    }
}
