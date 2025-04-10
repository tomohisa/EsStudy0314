using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Aggregates;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record CreateQuestionGroup(string Name) : ICommandWithHandler<CreateQuestionGroup, QuestionGroupProjector>
{
    public PartitionKeys SpecifyPartitionKeys(CreateQuestionGroup command) => 
        PartitionKeys.Generate<QuestionGroupProjector>();

    public ResultBox<EventOrNone> Handle(CreateQuestionGroup command, ICommandContext<IAggregatePayload> context)
        => context.GetAggregate()
            .Conveyor(aggregate => {
                var groupId = aggregate.PartitionKeys.AggregateId;
                return EventOrNone.Event(new QuestionGroupCreated(groupId, command.Name, new List<Guid>()));
            });
}
