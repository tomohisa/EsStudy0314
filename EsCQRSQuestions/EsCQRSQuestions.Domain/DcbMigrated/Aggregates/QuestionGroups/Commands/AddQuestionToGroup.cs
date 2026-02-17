using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record AddQuestionToGroup(Guid QuestionGroupId, Guid QuestionId, int Order) : ICommandWithHandler<AddQuestionToGroup>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(AddQuestionToGroup command, ICommandContext context)
    {
        var tag = new QuestionGroupTag(command.QuestionGroupId);
        var stateResult = await context.GetStateAsync<QuestionGroupProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not QuestionGroup group)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("QuestionGroup not found"));
        }

        return group.Questions.Any(q => q.QuestionId == command.QuestionId)
            ? ResultBox.FromException<EventOrNone>(new ArgumentException($"Question {command.QuestionId} is already in group"))
            : EventOrNone.EventWithTags(new QuestionAddedToGroup(command.QuestionGroupId, command.QuestionId, command.Order),
                tag,
                new QuestionTag(command.QuestionId));
    }
}
