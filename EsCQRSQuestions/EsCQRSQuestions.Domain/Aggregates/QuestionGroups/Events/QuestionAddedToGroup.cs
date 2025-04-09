using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionAddedToGroup(Guid QuestionId, int Order) : IEventPayload;