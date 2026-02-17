using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Commands;

[GenerateSerializer]
public record UpdateUserNameCommand(Guid ActiveUsersId, string ConnectionId, string Name) : ICommandWithHandler<UpdateUserNameCommand>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(UpdateUserNameCommand command, ICommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.ConnectionId))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Connection ID cannot be empty"));
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Name cannot be empty"));
        }

        var tag = new ActiveUsersTag(command.ActiveUsersId);
        var stateResult = await context.GetStateAsync<ActiveUsersProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not ActiveUsersAggregate activeUsers)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Active Users aggregate not found"));
        }

        if (!activeUsers.Users.Any(u => u.ConnectionId == command.ConnectionId))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("User not found"));
        }

        return EventOrNone.EventWithTags(new UserNameUpdated(command.ConnectionId, command.Name, DateTime.UtcNow), tag);
    }
}
