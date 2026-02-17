using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;

[GenerateSerializer]
public record ActiveUser(string ConnectionId, string? Name, DateTime ConnectedAt, DateTime LastActivityAt);

[GenerateSerializer]
public record ActiveUsersAggregate(Guid ActiveUsersId, List<ActiveUser> Users, int TotalCount) : ITagStatePayload;
