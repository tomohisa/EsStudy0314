using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record UpdateQuestionGroupIdCommand(Guid QuestionId, Guid QuestionGroupId) : ICommandWithHandler<UpdateQuestionGroupIdCommand>
{
    public static Task<ResultBox<EventOrNone>> HandleAsync(UpdateQuestionGroupIdCommand command, ICommandContext context)
    {
        if (command.QuestionGroupId == Guid.Empty)
        {
            return Task.FromResult(ResultBox.FromException<EventOrNone>(new ArgumentException("QuestionGroupId is required")));
        }

        return Task.FromResult(EventOrNone.EventWithTags(
            new QuestionGroupIdUpdated(command.QuestionGroupId),
            new QuestionTag(command.QuestionId),
            new QuestionGroupTag(command.QuestionGroupId)));
    }
}
