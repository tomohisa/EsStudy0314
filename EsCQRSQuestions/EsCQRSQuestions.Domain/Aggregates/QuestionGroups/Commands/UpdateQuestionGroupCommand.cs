using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using ResultBoxes;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Command.Executor;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands
{
    /// <summary>
    /// Command to update the name of an existing question group.
    /// </summary>
    [GenerateSerializer]
    public record UpdateQuestionGroupCommand(Guid GroupId, string NewName)
        : ICommandWithHandler<UpdateQuestionGroupCommand, QuestionGroupProjector, QuestionGroup> // Enforce state
    {
        public PartitionKeys SpecifyPartitionKeys(UpdateQuestionGroupCommand command)
            => PartitionKeys.Existing<QuestionGroupProjector>(command.GroupId);

        public ResultBox<EventOrNone> Handle(UpdateQuestionGroupCommand command, ICommandContext<QuestionGroup> context)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(command.NewName))
            {
                return new ArgumentException("Group name cannot be empty.", nameof(command.NewName));
            }

            // Check if the name actually changed
            if (context.GetAggregate().GetPayload().Name == command.NewName)
            {
                return EventOrNone.None; // No change needed
            }

            return EventOrNone.Event(new QuestionGroupUpdated(
                command.GroupId,
                command.NewName
            ));
        }
    }
}
