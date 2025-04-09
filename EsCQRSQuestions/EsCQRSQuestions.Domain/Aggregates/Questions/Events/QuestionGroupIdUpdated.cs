using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Events;

[GenerateSerializer]
public record QuestionGroupIdUpdated(Guid QuestionGroupId) : IEventPayload;