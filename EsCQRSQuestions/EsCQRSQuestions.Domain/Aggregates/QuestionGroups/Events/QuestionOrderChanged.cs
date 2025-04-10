using Sekiban.Pure.Events;
using System;
using System.Collections.Generic;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that the order of a question within a group has changed.
    /// </summary>
    [GenerateSerializer, EventPayload("questiongroup.question.orderchanged", SekibanVersion = 1)]
    public record QuestionOrderChanged(
        Guid GroupId, // Aggregate ID
        Guid QuestionId,
        int NewOrder,
        List<Guid> UpdatedOrder // The full ordered list of QuestionIds after the change
        ) : IEventPayload;
}
