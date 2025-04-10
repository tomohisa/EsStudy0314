using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using ResultBoxes;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Command.Executor; // Required for ICommandContext

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands
{
    /// <summary>
    /// Command to change the order of a question within an existing question group.
    /// </summary>
    [GenerateSerializer]
    public record ChangeQuestionOrderInGroupCommand(Guid GroupId, Guid QuestionId, int NewOrder)
        : ICommandWithHandler<ChangeQuestionOrderInGroupCommand, QuestionGroupProjector, QuestionGroup> // Enforce state
    {
        public PartitionKeys SpecifyPartitionKeys(ChangeQuestionOrderInGroupCommand command)
            => PartitionKeys.Existing<QuestionGroupProjector>(command.GroupId);

        public ResultBox<EventOrNone> Handle(ChangeQuestionOrderInGroupCommand command, ICommandContext<QuestionGroup> context)
        {
            var group = context.GetAggregate().GetPayload();
            var questionToMove = group.Questions.FirstOrDefault(q => q.QuestionId == command.QuestionId);

            // Validate command parameters
            if (questionToMove == null)
            {
                return new ArgumentException($"Question with ID {command.QuestionId} not found in group {command.GroupId}.", nameof(command.QuestionId));
            }
            if (command.NewOrder < 0 || command.NewOrder >= group.Questions.Count)
            {
                return new ArgumentOutOfRangeException(nameof(command.NewOrder), $"New order {command.NewOrder} is out of bounds for group size {group.Questions.Count}.");
            }
            if (questionToMove.Order == command.NewOrder)
            {
                return EventOrNone.None; // No change needed
            }

            // Calculate the final order after applying the change (using the payload logic for simulation)
            var simulatedGroup = group.ChangeQuestionOrder(command.QuestionId, command.NewOrder);
            var updatedOrder = simulatedGroup.Questions.OrderBy(q => q.Order).Select(q => q.QuestionId).ToList();


            return EventOrNone.Event(new QuestionOrderChanged(
                command.GroupId,
                command.QuestionId,
                command.NewOrder,
                updatedOrder
            ));
        }
    }
}
