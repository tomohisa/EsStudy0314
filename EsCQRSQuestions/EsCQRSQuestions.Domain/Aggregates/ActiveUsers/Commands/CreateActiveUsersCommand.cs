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
    // Fixed aggregate ID for the ActiveUsers aggregate
    private static readonly Guid FixedActiveUsersId = Guid.Parse("0195a6f7-dfff-75a7-b99f-36a0552a8eca");
    
    public PartitionKeys SpecifyPartitionKeys(CreateActiveUsersCommand command) => 
        PartitionKeys.Existing<ActiveUsersProjector>(FixedActiveUsersId);

    public ResultBox<EventOrNone> Handle(CreateActiveUsersCommand command, ICommandContext<IAggregatePayload> context)
    {
        // Create the event
        return EventOrNone.Event(new ActiveUsersCreated());
    }
}
