using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using ResultBoxes;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Commands;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using System;
using System.Linq;
using Sekiban.Pure.Aggregates; // Required for ICommandContext

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands
{
    /// <summary>
    /// Command to remove a question from an existing question group.
    /// </summary>
    [GenerateSerializer]
    public record RemoveQuestionFromGroupCommand(Guid GroupId, Guid QuestionId)
        : ICommandWithHandler<RemoveQuestionFromGroupCommand, QuestionGroupProjector, QuestionGroup> // Enforce state
    {
        public PartitionKeys SpecifyPartitionKeys(RemoveQuestionFromGroupCommand command)
            => PartitionKeys.Existing<QuestionGroupProjector>(command.GroupId);

        public ResultBox<EventOrNone> Handle(RemoveQuestionFromGroupCommand command, ICommandContext<QuestionGroup> context)
        {
            var group = context.GetAggregate().GetPayload();

            // Check if the question exists in the group
            if (!group.Questions.Any(q => q.QuestionId == command.QuestionId))
            {
                return EventOrNone.None; // Question not in group, no event needed
            }

            // Emit the removal event. The projector will handle reordering.
            return EventOrNone.Event(new QuestionRemovedFromGroup(
                command.GroupId,
                command.QuestionId
            ));
        }
    }
}
