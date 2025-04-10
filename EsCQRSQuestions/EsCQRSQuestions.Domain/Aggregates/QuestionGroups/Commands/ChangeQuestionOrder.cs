using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record ChangeQuestionOrder(Guid QuestionGroupId, Guid QuestionId, int NewOrder) : 
    ICommandWithHandler<ChangeQuestionOrder, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(ChangeQuestionOrder command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(ChangeQuestionOrder command, ICommandContext<QuestionGroup> context)
        => context.GetAggregate()
            .Conveyor(aggregate => aggregate.Payload.Questions.Any(q => q.QuestionId == command.QuestionId)
                ? EventOrNone.Event(new QuestionOrderChanged(command.QuestionId, command.NewOrder))
                : new ArgumentException($"Question {command.QuestionId} is not in group"));
}