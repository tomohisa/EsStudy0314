using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;

[GenerateSerializer]
public record ActiveUsersCreated(Guid ActiveUsersId) : IEventPayload;

[GenerateSerializer]
public record UserConnected(string ConnectionId, string? Name, DateTime ConnectedAt) : IEventPayload;

[GenerateSerializer]
public record UserDisconnected(string ConnectionId, DateTime DisconnectedAt) : IEventPayload;

[GenerateSerializer]
public record UserNameUpdated(string ConnectionId, string Name, DateTime UpdatedAt) : IEventPayload;
