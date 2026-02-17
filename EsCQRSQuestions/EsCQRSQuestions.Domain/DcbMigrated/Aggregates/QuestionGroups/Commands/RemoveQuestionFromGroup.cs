using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record RemoveQuestionFromGroup(Guid QuestionGroupId, Guid QuestionId) : ICommandWithHandler<RemoveQuestionFromGroup>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(RemoveQuestionFromGroup command, ICommandContext context)
    {
        var tag = new QuestionGroupTag(command.QuestionGroupId);
        var stateResult = await context.GetStateAsync<QuestionGroupProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not QuestionGroup group)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("QuestionGroup not found"));
        }

        return group.Questions.Any(q => q.QuestionId == command.QuestionId)
            ? EventOrNone.EventWithTags(new QuestionRemovedFromGroup(command.QuestionGroupId, command.QuestionId), tag,
                new QuestionTag(command.QuestionId))
            : ResultBox.FromException<EventOrNone>(new ArgumentException($"Question {command.QuestionId} is not in group"));
    }
}
