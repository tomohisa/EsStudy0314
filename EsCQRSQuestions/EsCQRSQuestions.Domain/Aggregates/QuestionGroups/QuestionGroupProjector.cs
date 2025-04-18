using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Events;
using Sekiban.Pure.Projectors;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups
{
    /// <summary>
    /// Projector for the Question Group aggregate. Applies events to build the current state.
    /// </summary>
    public class QuestionGroupProjector : IAggregateProjector
    {
        public IAggregatePayload Project(IAggregatePayload payload, IEvent ev)
            => (payload, ev.GetPayload()) switch
            {
                // Initial creation
                (EmptyAggregatePayload, QuestionGroupCreated e) =>
                    new QuestionGroup(
                        e.Name,
                        e.UniqueCode, // 新規追加：UniqueCodeの適用
                        e.InitialQuestionIds?.Select((id, index) => new QuestionReference(id, index)).ToList() ?? new()
                    ),

                // Update name
                (QuestionGroup group, QuestionGroupUpdated e) =>
                    group.UpdateName(e.NewName),

                // Add question
                (QuestionGroup group, QuestionAddedToGroup e) =>
                    group.AddQuestion(e.QuestionId), // Payload method handles order and duplicates

                // Remove question
                (QuestionGroup group, QuestionRemovedFromGroup e) =>
                    group.RemoveQuestion(e.QuestionId), // Payload method handles reordering

                // Change question order
                (QuestionGroup group, QuestionOrderChanged e) =>
                    group.ChangeQuestionOrder(e.QuestionId, e.NewOrder), // Payload method handles reordering

                // Deletion - Results in an Empty Payload to signify deletion
                (QuestionGroup group, QuestionGroupDeleted) =>
                    new DeletedQuestionGroup(group.Name,group.UniqueCode, group.Questions,ev.GetSortableUniqueId().GetTicks()), // 新規追加：UniqueCodeの適用

                // Default: No change for unhandled events or states
                _ => payload
            };
    }
}
