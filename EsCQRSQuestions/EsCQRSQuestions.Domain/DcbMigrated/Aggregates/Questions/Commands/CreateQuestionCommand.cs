using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record CreateQuestionCommand(
    string Text,
    List<QuestionOption> Options,
    Guid QuestionGroupId,
    bool AllowMultipleResponses = false
) : ICommandWithHandler<CreateQuestionCommand>
{
    public static Task<ResultBox<EventOrNone>> HandleAsync(CreateQuestionCommand command, ICommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
        {
            return Task.FromResult(ResultBox.FromException<EventOrNone>(new ArgumentException("Question text cannot be empty")));
        }

        if (command.Options is null || command.Options.Count < 2)
        {
            return Task.FromResult(ResultBox.FromException<EventOrNone>(new ArgumentException("Question must have at least two options")));
        }

        var optionIds = command.Options.Select(o => o.Id).ToList();
        if (optionIds.Count != optionIds.Distinct().Count())
        {
            return Task.FromResult(ResultBox.FromException<EventOrNone>(new ArgumentException("Option IDs must be unique")));
        }

        if (command.QuestionGroupId == Guid.Empty)
        {
            return Task.FromResult(ResultBox.FromException<EventOrNone>(new ArgumentException("QuestionGroupId is required")));
        }

        var questionId = Guid.CreateVersion7();
        var questionTag = new QuestionTag(questionId);
        var groupTag = new QuestionGroupTag(command.QuestionGroupId);

        return Task.FromResult(EventOrNone.EventWithTags(
            new QuestionCreated(questionId, command.Text, command.Options, command.QuestionGroupId,
                command.AllowMultipleResponses),
            questionTag,
            groupTag));
    }
}
