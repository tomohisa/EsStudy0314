using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Events;

[GenerateSerializer]
public record QuestionCreated(Guid QuestionId, string Text, List<QuestionOption> Options, Guid QuestionGroupId,
    bool AllowMultipleResponses = false) : IEventPayload;

[GenerateSerializer]
public record QuestionUpdated(string Text, List<QuestionOption> Options, bool AllowMultipleResponses = false) : IEventPayload;

[GenerateSerializer]
public record QuestionDeleted() : IEventPayload;

[GenerateSerializer]
public record QuestionDisplayStarted() : IEventPayload;

[GenerateSerializer]
public record QuestionDisplayStopped() : IEventPayload;

[GenerateSerializer]
public record ResponseAdded(
    Guid ResponseId,
    string? ParticipantName,
    string SelectedOptionId,
    string? Comment,
    DateTime Timestamp,
    string ClientId) : IEventPayload;

[GenerateSerializer]
public record ResponseCommentUpdated(
    Guid ResponseId,
    string ClientId,
    string? Comment,
    DateTime Timestamp) : IEventPayload;

[GenerateSerializer]
public record QuestionGroupIdUpdated(Guid QuestionGroupId) : IEventPayload;
