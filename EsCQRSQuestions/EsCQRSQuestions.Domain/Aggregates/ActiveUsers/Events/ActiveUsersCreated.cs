using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;

[GenerateSerializer]
public record ActiveUsersCreated() : IEventPayload;
