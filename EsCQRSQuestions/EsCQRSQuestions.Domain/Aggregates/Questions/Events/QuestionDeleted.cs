using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Events;

[GenerateSerializer]
public record QuestionDeleted() : IEventPayload;
