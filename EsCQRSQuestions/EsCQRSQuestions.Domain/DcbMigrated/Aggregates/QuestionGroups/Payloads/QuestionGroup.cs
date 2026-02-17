using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

[GenerateSerializer]
public record QuestionReference(Guid QuestionId, int Order);

[GenerateSerializer]
public record QuestionGroup(
    Guid GroupId,
    string Name,
    string UniqueCode,
    List<QuestionReference> Questions) : ITagStatePayload;

[GenerateSerializer]
public record DeletedQuestionGroup(
    string Name,
    string UniqueCode,
    List<QuestionReference> Questions,
    DateTime DeletedAt) : ITagStatePayload;
