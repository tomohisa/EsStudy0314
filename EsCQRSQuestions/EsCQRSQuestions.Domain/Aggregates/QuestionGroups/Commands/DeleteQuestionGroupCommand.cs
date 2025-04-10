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
    /// Command to delete an existing question group.
    /// </summary>
    [GenerateSerializer]
    public record DeleteQuestionGroupCommand(Guid GroupId)
        : ICommandWithHandler<DeleteQuestionGroupCommand, QuestionGroupProjector, QuestionGroup> // Enforce state
    {
        public PartitionKeys SpecifyPartitionKeys(DeleteQuestionGroupCommand command)
            => PartitionKeys.Existing<QuestionGroupProjector>(command.GroupId);

        public ResultBox<EventOrNone> Handle(DeleteQuestionGroupCommand command, ICommandContext<QuestionGroup> context)
        {
            // The state constraint already ensures the group exists.
            // We just need to emit the deletion event.
            return EventOrNone.Event(new QuestionGroupDeleted(command.GroupId));
        }
    }
}
