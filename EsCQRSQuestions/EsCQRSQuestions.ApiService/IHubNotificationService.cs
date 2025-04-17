namespace EsCQRSQuestions.ApiService;

/// <summary>
/// Interface for hub notification services
/// </summary>
public interface IHubNotificationService
{
    Task NotifyAllClientsAsync(string method, object data);
    Task NotifyAdminsAsync(string method, object data);
    Task NotifyParticipantsAsync(string method, object data);
    Task NotifyUniqueCodeGroupAsync(string uniqueCode, string method, object data);
}
