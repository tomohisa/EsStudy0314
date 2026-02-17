using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record StopDisplayCommand(Guid QuestionId) : ICommandWithHandler<StopDisplayCommand>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(StopDisplayCommand command, ICommandContext context)
    {
        var tag = new QuestionTag(command.QuestionId);
        var stateResult = await context.GetStateAsync<QuestionProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not Question question)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Question not found"));
        }

        if (!question.IsDisplayed)
        {
            return ResultBox.FromException<EventOrNone>(new InvalidOperationException("Question is not currently being displayed"));
        }

        return EventOrNone.EventWithTags(new QuestionDisplayStopped(), tag, new QuestionGroupTag(question.QuestionGroupId));
    }
}
