using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Queries;

namespace EsCQRSQuestions.AdminWeb;

public class ActiveUsersApiClient(HttpClient httpClient)
{
    // Get active users by ID
    public async Task<ActiveUsersQuery.ActiveUsersRecord?> GetActiveUsersAsync(Guid activeUsersId, string? waitForSortableUniqueId = null, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? $"/api/activeusers/{activeUsersId}"
            : $"/api/activeusers/{activeUsersId}?waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";
            
        return await httpClient.GetFromJsonAsync<ActiveUsersQuery.ActiveUsersRecord?>(requestUri, cancellationToken);
    }
}
