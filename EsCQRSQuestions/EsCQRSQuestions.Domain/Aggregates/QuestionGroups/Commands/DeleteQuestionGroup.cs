using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Aggregates;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record DeleteQuestionGroup(Guid QuestionGroupId) : 
    ICommandWithHandler<DeleteQuestionGroup, QuestionGroupProjector>
{
    public PartitionKeys SpecifyPartitionKeys(DeleteQuestionGroup command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(DeleteQuestionGroup command, ICommandContext<IAggregatePayload> context)
        => EventOrNone.Event(new QuestionGroupDeleted());
}