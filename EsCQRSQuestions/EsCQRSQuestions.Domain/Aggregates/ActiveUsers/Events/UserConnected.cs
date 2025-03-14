using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;

[GenerateSerializer]
public record UserConnected(
    string ConnectionId,
    string? Name,
    DateTime ConnectedAt
) : IEventPayload;
