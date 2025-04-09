using Sekiban.Pure.Aggregates;
using EsCQRSQuestions.Domain.ValueObjects;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

[GenerateSerializer]
public record QuestionGroup(
    string Name,
    List<QuestionReference> Questions
) : IAggregatePayload;

[GenerateSerializer]
public record DeletedQuestionGroup(
    string Name,
    List<QuestionReference> Questions
) : IAggregatePayload;