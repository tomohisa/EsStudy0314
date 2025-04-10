using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Aggregates;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record CreateQuestionGroupCommand(string Name, List<Guid>? InitialQuestionIds = null) : 
    ICommandWithHandler<CreateQuestionGroupCommand, QuestionGroupProjector>
{
    public PartitionKeys SpecifyPartitionKeys(CreateQuestionGroupCommand command) => 
        PartitionKeys.Generate<QuestionGroupProjector>(); // Generates a new Aggregate ID

    public ResultBox<EventOrNone> Handle(CreateQuestionGroupCommand command, ICommandContext<IAggregatePayload> context)
        => context.GetAggregate()
            .Conveyor(aggregate => {
                // Basic validation
                if (string.IsNullOrWhiteSpace(command.Name))
                {
                    return new ArgumentException("Group name cannot be empty.", nameof(command.Name));
                }

                // Ensure we are creating a new aggregate (current state is EmptyAggregatePayload)
                if (aggregate.Payload is not EmptyAggregatePayload)
                {
                    return new InvalidOperationException("Cannot create a group that already exists.");
                }

                var newGroupId = aggregate.PartitionKeys.AggregateId;

                return EventOrNone.Event(new QuestionGroupCreated(
                    newGroupId,
                    command.Name,
                    command.InitialQuestionIds ?? new List<Guid>()
                ));
            });
}
