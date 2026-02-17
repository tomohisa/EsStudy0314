using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record UpdateQuestionCommand(
    Guid QuestionId,
    string Text,
    List<QuestionOption> Options,
    bool AllowMultipleResponses = false
) : ICommandWithHandler<UpdateQuestionCommand>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(UpdateQuestionCommand command, ICommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("質問テキストは空にできません"));
        }

        if (command.Options is null || command.Options.Count < 2)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("質問には少なくとも2つの選択肢が必要です"));
        }

        var optionIds = command.Options.Select(o => o.Id).ToList();
        if (optionIds.Count != optionIds.Distinct().Count())
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("選択肢のIDは重複できません"));
        }

        var tag = new QuestionTag(command.QuestionId);
        var stateResult = await context.GetStateAsync<QuestionProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not Question question)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Question not found"));
        }

        if (question.IsDisplayed)
        {
            return ResultBox.FromException<EventOrNone>(new InvalidOperationException("表示中の質問は更新できません。表示を停止してから編集してください。"));
        }

        return EventOrNone.EventWithTags(
            new QuestionUpdated(command.Text, command.Options, command.AllowMultipleResponses),
            tag,
            new QuestionGroupTag(question.QuestionGroupId));
    }
}
