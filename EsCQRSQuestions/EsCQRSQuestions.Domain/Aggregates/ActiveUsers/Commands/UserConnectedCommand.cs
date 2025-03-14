using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;
using ResultBoxes;
using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Commands;

[GenerateSerializer]
public record UserConnectedCommand(
    Guid ActiveUsersId,
    string ConnectionId,
    string? Name
) : ICommandWithHandler<UserConnectedCommand, ActiveUsersProjector>
{
    public PartitionKeys SpecifyPartitionKeys(UserConnectedCommand command) => 
        PartitionKeys.Existing<ActiveUsersProjector>(command.ActiveUsersId);

    public ResultBox<EventOrNone> Handle(UserConnectedCommand command, ICommandContext<IAggregatePayload> context)
    {
        // Validate the command
        if (string.IsNullOrWhiteSpace(command.ConnectionId))
        {
            return new ArgumentException("Connection ID cannot be empty");
        }
        
        // Check if the aggregate exists
        var aggregate = context.GetAggregate().GetValue();
        if (aggregate.GetPayload() is not ActiveUsersAggregate)
        {
            return new ArgumentException("Active Users aggregate not found");
        }
        
        // Create the event
        return EventOrNone.Event(new UserConnected(
            command.ConnectionId,
            command.Name,
            DateTime.UtcNow));
    }
}
