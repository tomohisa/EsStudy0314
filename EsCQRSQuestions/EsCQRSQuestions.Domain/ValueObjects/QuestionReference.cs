namespace EsCQRSQuestions.Domain.ValueObjects;

[GenerateSerializer]
public record QuestionReference(
    Guid QuestionId,
    int Order
);