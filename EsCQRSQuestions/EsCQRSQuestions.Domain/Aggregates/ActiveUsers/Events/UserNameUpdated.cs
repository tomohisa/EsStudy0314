using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;

[GenerateSerializer]
public record UserNameUpdated(
    string ConnectionId,
    string Name,
    DateTime UpdatedAt
) : IEventPayload;
