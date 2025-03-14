using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Queries;

namespace EsCQRSQuestions.AdminWeb;

public class ActiveUsersApiClient(HttpClient httpClient)
{
    // Get active users by ID
    public async Task<ActiveUsersQuery.ActiveUsersRecord?> GetActiveUsersAsync(Guid activeUsersId, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<ActiveUsersQuery.ActiveUsersRecord?>($"/api/activeusers/{activeUsersId}", cancellationToken);
    }
}
