using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Events;

[GenerateSerializer]
public record ResponseAdded(
    Guid ResponseId,
    string? ParticipantName,
    string SelectedOptionId,
    string? Comment,
    DateTime Timestamp,
    string ClientId
) : IEventPayload;
