using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Aggregates;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record UpdateQuestionGroupName(Guid QuestionGroupId, string Name) : 
    ICommandWithHandler<UpdateQuestionGroupName, QuestionGroupProjector>
{
    public PartitionKeys SpecifyPartitionKeys(UpdateQuestionGroupName command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(UpdateQuestionGroupName command, ICommandContext<IAggregatePayload> context)
        => EventOrNone.Event(new QuestionGroupNameUpdated(command.Name));
}