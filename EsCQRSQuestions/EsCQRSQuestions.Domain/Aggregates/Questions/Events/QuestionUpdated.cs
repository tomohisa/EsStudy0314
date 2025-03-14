using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Events;

[GenerateSerializer]
public record QuestionUpdated(
    string Text,
    List<QuestionOption> Options
) : IEventPayload;
