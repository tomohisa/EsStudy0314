using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record DeleteQuestionGroup(Guid QuestionGroupId) : ICommandWithHandler<DeleteQuestionGroup>
{
    public static Task<ResultBox<EventOrNone>> HandleAsync(DeleteQuestionGroup command, ICommandContext context) =>
        Task.FromResult(EventOrNone.EventWithTags(new QuestionGroupDeleted(command.QuestionGroupId),
            new QuestionGroupTag(command.QuestionGroupId)));
}
