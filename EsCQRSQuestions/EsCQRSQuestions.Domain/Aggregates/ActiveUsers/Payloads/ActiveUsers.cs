using Sekiban.Pure.Aggregates;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;

[GenerateSerializer]
public record ActiveUsersAggregate(
    List<ActiveUser> Users,
    int TotalCount
) : IAggregatePayload;

[GenerateSerializer]
public record ActiveUser(
    string ConnectionId,
    string? Name,
    DateTime ConnectedAt,
    DateTime LastActivityAt
);
