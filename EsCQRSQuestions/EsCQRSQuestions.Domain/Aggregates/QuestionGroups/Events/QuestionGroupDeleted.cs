using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionGroupDeleted() : IEventPayload;