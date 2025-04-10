using Sekiban.Pure.Events;
using System;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that a question has been added to a group.
    /// </summary>
    [GenerateSerializer, EventPayload("questiongroup.question.added", SekibanVersion = 1)]
    public record QuestionAddedToGroup(
        Guid GroupId, // Aggregate ID
        Guid QuestionId,
        int Order // The order assigned to the question within the group
        ) : IEventPayload;
}
