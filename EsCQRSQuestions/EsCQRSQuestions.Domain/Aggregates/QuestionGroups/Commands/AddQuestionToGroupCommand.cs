using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record AddQuestionToGroupCommand(Guid GroupId, Guid QuestionId) : 
    ICommandWithHandler<AddQuestionToGroupCommand, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(AddQuestionToGroupCommand command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.GroupId);

    public ResultBox<EventOrNone> Handle(AddQuestionToGroupCommand command, ICommandContext<QuestionGroup> context)
        => context.GetAggregate()
            .Conveyor(aggregate => {
                if (aggregate.Payload.Questions.Any(q => q.QuestionId == command.QuestionId))
                {
                    return EventOrNone.None;
                }
                
                var nextOrder = aggregate.Payload.Questions.Count > 0 
                    ? aggregate.Payload.Questions.Max(q => q.Order) + 1 
                    : 0;
                
                return EventOrNone.Event(new QuestionAddedToGroup(command.GroupId, command.QuestionId, nextOrder));
            });
}
