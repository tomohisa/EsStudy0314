using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;
using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Events;
using Sekiban.Pure.Projectors;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers;

public class ActiveUsersProjector : IAggregateProjector
{
    public IAggregatePayload Project(IAggregatePayload payload, IEvent ev)
        => (payload, ev.GetPayload()) switch
        {
            // Create a new ActiveUsers aggregate
            (EmptyAggregatePayload, ActiveUsersCreated _) => new ActiveUsersAggregate(
                new List<ActiveUser>(),
                0),
            
            // Add a new user connection
            (ActiveUsersAggregate activeUsers, UserConnected connected) => activeUsers with
            {
                Users = activeUsers.Users
                    .Where(u => u.ConnectionId != connected.ConnectionId)
                    .Append(new ActiveUser(
                        connected.ConnectionId,
                        connected.Name,
                        connected.ConnectedAt,
                        connected.ConnectedAt))
                    .ToList(),
                TotalCount = activeUsers.Users.Count(u => u.ConnectionId != connected.ConnectionId) + 1
            },
            
            // Remove a user connection
            (ActiveUsersAggregate activeUsers, UserDisconnected disconnected) => activeUsers with
            {
                Users = activeUsers.Users
                    .Where(u => u.ConnectionId != disconnected.ConnectionId)
                    .ToList(),
                TotalCount = activeUsers.Users.Count(u => u.ConnectionId != disconnected.ConnectionId)
            },
            
            // Update a user's name
            (ActiveUsersAggregate activeUsers, UserNameUpdated nameUpdated) => activeUsers with
            {
                Users = activeUsers.Users
                    .Select(u => u.ConnectionId == nameUpdated.ConnectionId
                        ? u with { Name = nameUpdated.Name, LastActivityAt = nameUpdated.UpdatedAt }
                        : u)
                    .ToList()
            },
            
            // Default case - return the payload unchanged
            _ => payload
        };
}
