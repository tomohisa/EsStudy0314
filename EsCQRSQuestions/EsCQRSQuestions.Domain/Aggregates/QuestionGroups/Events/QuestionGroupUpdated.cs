using Sekiban.Pure.Events;
using System;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that a question group's name has been updated.
    /// </summary>
    [GenerateSerializer, EventPayload("questiongroup.updated", SekibanVersion = 1)]
    public record QuestionGroupUpdated(
        Guid GroupId, // Aggregate ID
        string NewName
        ) : IEventPayload;
}
