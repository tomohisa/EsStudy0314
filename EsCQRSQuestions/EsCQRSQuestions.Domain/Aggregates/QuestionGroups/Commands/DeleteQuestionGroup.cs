using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record DeleteQuestionGroup(Guid QuestionGroupId) : 
    ICommandWithHandler<DeleteQuestionGroup, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(DeleteQuestionGroup command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(DeleteQuestionGroup command, ICommandContext<QuestionGroup> context)
        => context.GetAggregate()
            .Conveyor(_ => EventOrNone.Event(new QuestionGroupDeleted(command.QuestionGroupId)));
}
