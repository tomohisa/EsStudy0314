using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that a new question group has been created.
    /// </summary>
    [GenerateSerializer]
    public record QuestionGroupCreated(
        Guid GroupId, // Aggregate ID
        string Name,
        List<Guid> InitialQuestionIds // Optional: If created with initial questions
        ) : IEventPayload;
}
