using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;
using ResultBoxes;
using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Projectors;
using Sekiban.Pure.Query;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Queries;

[GenerateSerializer]
public record ActiveUsersQuery(Guid ActiveUsersId)
    : IMultiProjectionQuery<AggregateListProjector<ActiveUsersProjector>, ActiveUsersQuery, ActiveUsersQuery.ActiveUsersRecord>
{
    public static ResultBox<ActiveUsersRecord> HandleQuery(
        MultiProjectionState<AggregateListProjector<ActiveUsersProjector>> projection, 
        ActiveUsersQuery query, 
        IQueryContext context)
    {
        var aggregateResult = projection.Payload.Aggregates
            .Where(m => m.Key.AggregateId == query.ActiveUsersId)
            .Select(m => m.Value)
            .FirstOrDefault();

        if (aggregateResult == null)
        {
            return new ActiveUsersRecord(
                Guid.Empty,
                0,
                new List<ActiveUserRecord>());
        }

        var activeUsers = aggregateResult.GetPayload() as ActiveUsersAggregate;
        if (activeUsers == null)
        {
            return new ActiveUsersRecord(
                Guid.Empty,
                0,
                new List<ActiveUserRecord>());
        }
        
        return new ActiveUsersRecord(
            aggregateResult.PartitionKeys.AggregateId,
            activeUsers.TotalCount,
            activeUsers.Users.Select(u => new ActiveUserRecord(
                u.ConnectionId,
                u.Name,
                u.ConnectedAt,
                u.LastActivityAt)).ToList()
        ).ToResultBox();
    }

    [GenerateSerializer]
    public record ActiveUsersRecord(
        Guid ActiveUsersId,
        int TotalCount,
        List<ActiveUserRecord> Users
    );
    
    [GenerateSerializer]
    public record ActiveUserRecord(
        string ConnectionId,
        string? Name,
        DateTime ConnectedAt,
        DateTime LastActivityAt
    );
}
