using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;

[GenerateSerializer]
public record QuestionParticipantResponse(
    string ClientId,
    Guid LastResponseId,
    string LastSelectedOptionId,
    string? LastComment,
    DateTime LastTimestamp) : ITagStatePayload;
