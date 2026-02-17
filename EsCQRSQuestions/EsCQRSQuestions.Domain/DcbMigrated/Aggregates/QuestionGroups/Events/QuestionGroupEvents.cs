using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionGroupCreated(Guid GroupId, string Name, string UniqueCode = "", List<Guid>? InitialQuestionIds = null)
    : IEventPayload;

[GenerateSerializer]
public record QuestionGroupUpdated(Guid GroupId, string NewName) : IEventPayload;

[GenerateSerializer]
public record QuestionGroupDeleted(Guid GroupId) : IEventPayload;

[GenerateSerializer]
public record QuestionGroupNameUpdated(string Name) : IEventPayload;

[GenerateSerializer]
public record QuestionAddedToGroup(Guid GroupId, Guid QuestionId, int Order) : IEventPayload;

[GenerateSerializer]
public record QuestionRemovedFromGroup(Guid GroupId, Guid QuestionId) : IEventPayload;

[GenerateSerializer]
public record QuestionOrderChanged(Guid GroupId, Guid QuestionId, int NewOrder, List<Guid> UpdatedOrder) : IEventPayload;
