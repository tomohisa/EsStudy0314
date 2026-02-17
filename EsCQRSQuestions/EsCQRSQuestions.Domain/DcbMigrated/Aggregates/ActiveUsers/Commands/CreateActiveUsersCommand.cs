using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Commands;

[GenerateSerializer]
public record CreateActiveUsersCommand() : ICommandWithHandler<CreateActiveUsersCommand>
{
    private static readonly Guid FixedActiveUsersId = Guid.Parse("0195a6f7-dfff-75a7-b99f-36a0552a8eca");

    public static Task<ResultBox<EventOrNone>> HandleAsync(CreateActiveUsersCommand command, ICommandContext context)
    {
        var tag = new ActiveUsersTag(FixedActiveUsersId);
        return Task.FromResult(EventOrNone.EventWithTags(new ActiveUsersCreated(FixedActiveUsersId), tag));
    }
}
