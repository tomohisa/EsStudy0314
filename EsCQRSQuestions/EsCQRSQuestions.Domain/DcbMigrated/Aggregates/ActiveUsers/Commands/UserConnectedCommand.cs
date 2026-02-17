using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Commands;

[GenerateSerializer]
public record UserConnectedCommand(Guid ActiveUsersId, string ConnectionId, string? Name) : ICommandWithHandler<UserConnectedCommand>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(UserConnectedCommand command, ICommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.ConnectionId))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Connection ID cannot be empty"));
        }

        var tag = new ActiveUsersTag(command.ActiveUsersId);
        var stateResult = await context.GetStateAsync<ActiveUsersProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not ActiveUsersAggregate)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Active Users aggregate not found"));
        }

        return EventOrNone.EventWithTags(new UserConnected(command.ConnectionId, command.Name, DateTime.UtcNow), tag);
    }
}
