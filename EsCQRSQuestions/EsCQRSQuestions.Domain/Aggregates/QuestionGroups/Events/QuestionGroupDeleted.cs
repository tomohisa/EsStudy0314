using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that a question group has been deleted.
    /// Note: Consider using a state like "Deleted" in the payload instead if soft delete is preferred.
    /// </summary>
    [GenerateSerializer]
    public record QuestionGroupDeleted(
        Guid GroupId // Aggregate ID
        ) : IEventPayload;
}
