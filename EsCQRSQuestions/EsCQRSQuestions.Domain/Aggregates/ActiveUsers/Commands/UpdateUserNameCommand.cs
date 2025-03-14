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
public record UpdateUserNameCommand(
    Guid ActiveUsersId,
    string ConnectionId,
    string Name
) : ICommandWithHandler<UpdateUserNameCommand, ActiveUsersProjector>
{
    public PartitionKeys SpecifyPartitionKeys(UpdateUserNameCommand command) => 
        PartitionKeys.Existing<ActiveUsersProjector>(command.ActiveUsersId);

    public ResultBox<EventOrNone> Handle(UpdateUserNameCommand command, ICommandContext<IAggregatePayload> context)
    {
        // Validate the command
        if (string.IsNullOrWhiteSpace(command.ConnectionId))
        {
            return new ArgumentException("Connection ID cannot be empty");
        }
        
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new ArgumentException("Name cannot be empty");
        }
        
        // Check if the aggregate exists
        var aggregate = context.GetAggregate().GetValue();
        if (aggregate.GetPayload() is not ActiveUsersAggregate activeUsers)
        {
            return new ArgumentException("Active Users aggregate not found");
        }
        
        // Check if the user exists
        if (!activeUsers.Users.Any(u => u.ConnectionId == command.ConnectionId))
        {
            return new ArgumentException("User not found");
        }
        
        // Create the event
        return EventOrNone.Event(new UserNameUpdated(
            command.ConnectionId,
            command.Name,
            DateTime.UtcNow));
    }
}
