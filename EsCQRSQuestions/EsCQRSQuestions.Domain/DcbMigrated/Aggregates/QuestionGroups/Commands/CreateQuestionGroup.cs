using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record CreateQuestionGroup(string Name, string UniqueCode = "") : ICommandWithHandler<CreateQuestionGroup>
{
    public static Task<ResultBox<EventOrNone>> HandleAsync(CreateQuestionGroup command, ICommandContext context)
    {
        var groupId = Guid.CreateVersion7();
        var uniqueCode = string.IsNullOrEmpty(command.UniqueCode) ? GenerateRandomCode() : command.UniqueCode;

        return Task.FromResult(EventOrNone.EventWithTags(
            new QuestionGroupCreated(groupId, command.Name, uniqueCode),
            new QuestionGroupTag(groupId)));
    }

    private static string GenerateRandomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
