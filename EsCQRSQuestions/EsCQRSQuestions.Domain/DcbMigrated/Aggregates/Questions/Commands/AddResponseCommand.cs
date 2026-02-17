using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record AddResponseCommand(
    Guid QuestionId,
    string? ParticipantName,
    string SelectedOptionId,
    string? Comment,
    string ClientId
) : ICommandWithHandler<AddResponseCommand>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(AddResponseCommand command, ICommandContext context)
    {
        var tag = new QuestionTag(command.QuestionId);
        var stateResult = await context.GetStateAsync<QuestionProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not Question question)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Question not found"));
        }

        if (!question.IsDisplayed)
        {
            return ResultBox.FromException<EventOrNone>(new InvalidOperationException("Cannot add a response to a question that is not being displayed"));
        }

        if (string.IsNullOrWhiteSpace(command.SelectedOptionId))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Selected option ID cannot be empty"));
        }

        if (!question.Options.Any(o => o.Id == command.SelectedOptionId))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException($"Option with ID '{command.SelectedOptionId}' does not exist"));
        }

        if (!question.AllowMultipleResponses && question.Responses.Any(r => r.ClientId == command.ClientId))
        {
            return ResultBox.FromException<EventOrNone>(new InvalidOperationException("Multiple responses are not allowed for this question"));
        }

        return EventOrNone.EventWithTags(
            new ResponseAdded(Guid.NewGuid(), command.ParticipantName, command.SelectedOptionId, command.Comment,
                DateTime.UtcNow, command.ClientId),
            tag,
            new QuestionGroupTag(question.QuestionGroupId));
    }
}
