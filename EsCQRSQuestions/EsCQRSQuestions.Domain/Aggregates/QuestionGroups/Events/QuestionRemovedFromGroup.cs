using Sekiban.Pure.Events;
using System;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that a question has been removed from a group.
    /// </summary>
    [GenerateSerializer, EventPayload("questiongroup.question.removed", SekibanVersion = 1)]
    public record QuestionRemovedFromGroup(
        Guid GroupId, // Aggregate ID
        Guid QuestionId
        ) : IEventPayload;
}
