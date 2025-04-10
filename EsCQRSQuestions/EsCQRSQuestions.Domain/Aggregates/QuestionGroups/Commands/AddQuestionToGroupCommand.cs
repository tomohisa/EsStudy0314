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
    /// Command to add a question to an existing question group.
    /// </summary>
    [GenerateSerializer]
    public record AddQuestionToGroupCommand(Guid GroupId, Guid QuestionId)
        : ICommandWithHandler<AddQuestionToGroupCommand, QuestionGroupProjector, QuestionGroup> // Enforce state
    {
        public PartitionKeys SpecifyPartitionKeys(AddQuestionToGroupCommand command)
            => PartitionKeys.Existing<QuestionGroupProjector>(command.GroupId);

        public ResultBox<EventOrNone> Handle(AddQuestionToGroupCommand command, ICommandContext<QuestionGroup> context)
        {
            var group = context.GetAggregate().GetPayload();

            // Check if question already exists in the group
            if (group.Questions.Any(q => q.QuestionId == command.QuestionId))
            {
                return EventOrNone.None; // Question already in group, no event needed
            }

            // Determine the order for the new question
            var nextOrder = group.Questions.Count > 0 ? group.Questions.Max(q => q.Order) + 1 : 0;

            return EventOrNone.Event(new QuestionAddedToGroup(
                command.GroupId,
                command.QuestionId,
                nextOrder
            ));
        }
    }
}
