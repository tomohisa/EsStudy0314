using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace EsCQRSQuestions.ApiService;

/// <summary>
/// Implementation of hub notification service for the QuestionHub
/// </summary>
public class HubNotificationService : IHubNotificationService
{
    private readonly IHubContext<QuestionHub> _hubContext;
    private readonly ILogger<HubNotificationService> _logger;

    public HubNotificationService(
        IHubContext<QuestionHub> hubContext,
        ILogger<HubNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
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

    // UniqueCodeグループへの通知
    public async Task NotifyUniqueCodeGroupAsync(string uniqueCode, string method, object data)
    {
        try
        {
            _logger.LogInformation($"NotifyUniqueCodeGroupAsync: UniqueCode={uniqueCode}, Method={method}");
            
            if (!string.IsNullOrWhiteSpace(uniqueCode))
            {
                await _hubContext.Clients.Group(uniqueCode).SendAsync(method, data);
                _logger.LogInformation($"Notification sent to group {uniqueCode}");
            }
            else
            {
                _logger.LogWarning("UniqueCode is empty, notification not sent");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in NotifyUniqueCodeGroupAsync: {ex.Message}");
        }
    }
}
