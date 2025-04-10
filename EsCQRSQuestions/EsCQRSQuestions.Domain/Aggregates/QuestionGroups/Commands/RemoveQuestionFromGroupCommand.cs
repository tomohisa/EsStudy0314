using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record RemoveQuestionFromGroupCommand(Guid GroupId, Guid QuestionId) : 
    ICommandWithHandler<RemoveQuestionFromGroupCommand, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(RemoveQuestionFromGroupCommand command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.GroupId);

    public ResultBox<EventOrNone> Handle(RemoveQuestionFromGroupCommand command, ICommandContext<QuestionGroup> context)
        => context.GetAggregate()
            .Conveyor(aggregate => 
                !aggregate.Payload.Questions.Any(q => q.QuestionId == command.QuestionId)
                    ? EventOrNone.None
                    : EventOrNone.Event(new QuestionRemovedFromGroup(command.GroupId, command.QuestionId)));
}
