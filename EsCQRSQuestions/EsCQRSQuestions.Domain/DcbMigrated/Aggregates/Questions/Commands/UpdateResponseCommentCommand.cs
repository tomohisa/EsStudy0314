using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record UpdateResponseCommentCommand(
    Guid QuestionId,
    string ClientId,
    string? Comment
) : ICommandWithHandler<UpdateResponseCommentCommand>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(UpdateResponseCommentCommand command, ICommandContext context)
    {
        var tag = new QuestionTag(command.QuestionId);
        var stateResult = await context.GetStateAsync<QuestionProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not Question question)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Question not found"));
        }

        if (!question.IsDisplayed)
        {
            return ResultBox.FromException<EventOrNone>(
                new InvalidOperationException("Cannot update a comment for a question that is not being displayed"));
        }

        if (string.IsNullOrWhiteSpace(command.ClientId))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Client ID cannot be empty"));
        }

        var targetResponse = question.Responses.LastOrDefault(r => r.ClientId == command.ClientId);
        if (targetResponse is null)
        {
            return ResultBox.FromException<EventOrNone>(
                new InvalidOperationException("Cannot update comment because no response exists for this participant"));
        }

        return EventOrNone.EventWithTags(
            new ResponseCommentUpdated(
                targetResponse.Id,
                command.ClientId,
                command.Comment,
                DateTime.UtcNow),
            tag,
            new QuestionGroupTag(question.QuestionGroupId));
    }
}
