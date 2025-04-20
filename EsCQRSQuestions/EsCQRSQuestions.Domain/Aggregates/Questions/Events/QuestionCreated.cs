using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Events;

[GenerateSerializer]
public record QuestionCreated(
    string Text,
    List<QuestionOption> Options,
    Guid QuestionGroupId,
    bool AllowMultipleResponses = false // 追加：複数回答を許可するかどうか
) : IEventPayload;
