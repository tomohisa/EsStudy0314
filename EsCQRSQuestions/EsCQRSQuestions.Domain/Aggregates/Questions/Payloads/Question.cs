using Sekiban.Pure.Aggregates;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;

[GenerateSerializer]
public record Question(
    string Text,
    List<QuestionOption> Options,
    bool IsDisplayed,
    List<QuestionResponse> Responses,
    Guid QuestionGroupId
) : IAggregatePayload;

[GenerateSerializer]
public record QuestionOption(
    string Id,
    string Text
);

[GenerateSerializer]
public record QuestionResponse(
    Guid Id,
    string? ParticipantName,
    string SelectedOptionId,
    string? Comment,
    DateTime Timestamp,
    string ClientId
);

[GenerateSerializer]
public record DeletedQuestion(
    string Text,
    List<QuestionOption> Options,
    bool IsDisplayed,
    List<QuestionResponse> Responses,
    Guid QuestionGroupId
) : IAggregatePayload;
