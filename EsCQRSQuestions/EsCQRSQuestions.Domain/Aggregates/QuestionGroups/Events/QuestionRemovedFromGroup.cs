using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that a question has been removed from a group.
    /// </summary>
    [GenerateSerializer]
    public record QuestionRemovedFromGroup(
        Guid GroupId, // Aggregate ID
        Guid QuestionId
        ) : IEventPayload;
}
