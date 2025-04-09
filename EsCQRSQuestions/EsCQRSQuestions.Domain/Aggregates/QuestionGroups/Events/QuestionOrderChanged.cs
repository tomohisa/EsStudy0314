using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionOrderChanged(Guid QuestionId, int NewOrder) : IEventPayload;