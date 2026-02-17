using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record DeleteQuestionCommand(Guid QuestionId) : ICommandWithHandler<DeleteQuestionCommand>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(DeleteQuestionCommand command, ICommandContext context)
    {
        var tag = new QuestionTag(command.QuestionId);
        var stateResult = await context.GetStateAsync<QuestionProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not Question question)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Question not found"));
        }

        if (question.IsDisplayed)
        {
            return ResultBox.FromException<EventOrNone>(new InvalidOperationException("Cannot delete a question that is currently being displayed"));
        }

        return EventOrNone.EventWithTags(new QuestionDeleted(), tag, new QuestionGroupTag(question.QuestionGroupId));
    }
}
