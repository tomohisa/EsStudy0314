using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Queries;

[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public record ActiveUsersQuery(Guid ActiveUsersId) :
    IMultiProjectionQuery<GenericTagMultiProjector<ActiveUsersProjector, ActiveUsersTag>, ActiveUsersQuery,
        ActiveUsersQuery.ActiveUsersRecord>,
    IWaitForSortableUniqueId
{
    public string? WaitForSortableUniqueId { get; init; }

    public static ResultBox<ActiveUsersRecord> HandleQuery(
        GenericTagMultiProjector<ActiveUsersProjector, ActiveUsersTag> projection,
        ActiveUsersQuery query,
        IQueryContext context)
    {
        var activeUsers = projection.GetCurrentTagStates().Values
            .Where(m => m.Payload is ActiveUsersAggregate)
            .Select(m => (ActiveUsersAggregate)m.Payload)
            .FirstOrDefault(m => m.ActiveUsersId == query.ActiveUsersId);

        if (activeUsers is null)
        {
            return new ActiveUsersRecord(Guid.Empty, 0, new List<ActiveUserRecord>());
        }

        return new ActiveUsersRecord(
            activeUsers.ActiveUsersId,
            activeUsers.TotalCount,
            activeUsers.Users.Select(u => new ActiveUserRecord(u.ConnectionId, u.Name, u.ConnectedAt, u.LastActivityAt))
                .ToList());
    }

    [GenerateSerializer]
    public record ActiveUsersRecord(Guid ActiveUsersId, int TotalCount, List<ActiveUserRecord> Users);

    [GenerateSerializer]
    public record ActiveUserRecord(string ConnectionId, string? Name, DateTime ConnectedAt, DateTime LastActivityAt);
}
