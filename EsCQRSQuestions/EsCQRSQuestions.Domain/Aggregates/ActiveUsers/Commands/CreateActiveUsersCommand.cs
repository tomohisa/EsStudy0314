using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using ResultBoxes;
using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Commands;

[GenerateSerializer]
public record CreateActiveUsersCommand() : ICommandWithHandler<CreateActiveUsersCommand, ActiveUsersProjector>
{
    public PartitionKeys SpecifyPartitionKeys(CreateActiveUsersCommand command) => 
        PartitionKeys.Generate<ActiveUsersProjector>();

    public ResultBox<EventOrNone> Handle(CreateActiveUsersCommand command, ICommandContext<IAggregatePayload> context)
    {
        // Create the event
        return EventOrNone.Event(new ActiveUsersCreated());
    }
}
