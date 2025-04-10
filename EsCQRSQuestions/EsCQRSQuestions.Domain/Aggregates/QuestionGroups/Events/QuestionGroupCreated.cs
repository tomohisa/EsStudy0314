using Sekiban.Pure.Events;
using System;
using System.Collections.Generic;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that a new question group has been created.
    /// </summary>
    [GenerateSerializer, EventPayload("questiongroup.created", SekibanVersion = 1)]
    public record QuestionGroupCreated(
        Guid GroupId, // Aggregate ID
        string Name,
        List<Guid> InitialQuestionIds // Optional: If created with initial questions
        ) : IEventPayload;
}
