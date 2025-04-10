using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record UpdateQuestionGroupCommand(Guid GroupId, string NewName) : 
    ICommandWithHandler<UpdateQuestionGroupCommand, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(UpdateQuestionGroupCommand command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.GroupId);

    public ResultBox<EventOrNone> Handle(UpdateQuestionGroupCommand command, ICommandContext<QuestionGroup> context)
        => context.GetAggregate()
            .Conveyor(aggregate => 
                string.IsNullOrWhiteSpace(command.NewName)
                    ? new ArgumentException("Group name cannot be empty.", nameof(command.NewName))
                    : aggregate.Payload.Name == command.NewName
                        ? EventOrNone.None
                        : EventOrNone.Event(new QuestionGroupUpdated(command.GroupId, command.NewName)));
}
