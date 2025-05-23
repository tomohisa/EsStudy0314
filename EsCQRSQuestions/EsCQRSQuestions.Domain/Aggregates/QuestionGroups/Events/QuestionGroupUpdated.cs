using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that a question group's name has been updated.
    /// </summary>
    [GenerateSerializer]
    public record QuestionGroupUpdated(
        Guid GroupId, // Aggregate ID
        string NewName
        ) : IEventPayload;
}
