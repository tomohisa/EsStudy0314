using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;
using Sekiban.Dcb.Events;
using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers;

public class ActiveUsersProjector : ITagProjector<ActiveUsersProjector>
{
    public static string ProjectorVersion => "1.0.0";
    public static string ProjectorName => nameof(ActiveUsersProjector);

    public static ITagStatePayload Project(ITagStatePayload current, Event ev) =>
        (current, ev.Payload) switch
        {
            (EmptyTagStatePayload, ActiveUsersCreated created) =>
                new ActiveUsersAggregate(created.ActiveUsersId, new List<ActiveUser>(), 0),
            (ActiveUsersAggregate activeUsers, UserConnected connected) => activeUsers with
            {
                Users = activeUsers.Users.Where(u => u.ConnectionId != connected.ConnectionId)
                    .Append(new ActiveUser(connected.ConnectionId, connected.Name, connected.ConnectedAt,
                        connected.ConnectedAt)).ToList(),
                TotalCount = activeUsers.Users.Count(u => u.ConnectionId != connected.ConnectionId) + 1
            },
            (ActiveUsersAggregate activeUsers, UserDisconnected disconnected) => activeUsers with
            {
                Users = activeUsers.Users.Where(u => u.ConnectionId != disconnected.ConnectionId).ToList(),
                TotalCount = activeUsers.Users.Count(u => u.ConnectionId != disconnected.ConnectionId)
            },
            (ActiveUsersAggregate activeUsers, UserNameUpdated updated) => activeUsers with
            {
                Users = activeUsers.Users
                    .Select(u => u.ConnectionId == updated.ConnectionId
                        ? u with { Name = updated.Name, LastActivityAt = updated.UpdatedAt }
                        : u)
                    .ToList()
            },
            _ => current
        };
}
