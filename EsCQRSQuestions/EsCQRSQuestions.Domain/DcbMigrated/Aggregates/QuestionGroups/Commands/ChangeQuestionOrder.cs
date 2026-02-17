using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record ChangeQuestionOrder(Guid QuestionGroupId, Guid QuestionId, int NewOrder) : ICommandWithHandler<ChangeQuestionOrder>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(ChangeQuestionOrder command, ICommandContext context)
    {
        var tag = new QuestionGroupTag(command.QuestionGroupId);
        var stateResult = await context.GetStateAsync<QuestionGroupProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not QuestionGroup group)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("QuestionGroup not found"));
        }

        if (!group.Questions.Any(q => q.QuestionId == command.QuestionId))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException($"Question {command.QuestionId} is not in group"));
        }

        var questions = group.Questions.ToList();
        var questionToMove = questions.First(q => q.QuestionId == command.QuestionId);
        questions.Remove(questionToMove);

        var updatedQuestion = questionToMove with { Order = command.NewOrder };
        var insertIndex = 0;
        while (insertIndex < questions.Count && questions[insertIndex].Order <= command.NewOrder)
        {
            insertIndex++;
        }

        questions.Insert(insertIndex, updatedQuestion);
        for (var i = 0; i < questions.Count; i++)
        {
            questions[i] = questions[i] with { Order = i };
        }

        var updatedOrder = questions.OrderBy(q => q.Order).Select(q => q.QuestionId).ToList();

        return EventOrNone.EventWithTags(
            new QuestionOrderChanged(command.QuestionGroupId, command.QuestionId, command.NewOrder, updatedOrder),
            tag,
            new QuestionTag(command.QuestionId));
    }
}
