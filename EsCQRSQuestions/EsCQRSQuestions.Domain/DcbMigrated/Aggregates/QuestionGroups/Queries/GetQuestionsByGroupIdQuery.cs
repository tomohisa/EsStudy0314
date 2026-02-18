using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Projections.Questions;
using ResultBoxes;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;

[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public record GetQuestionsByGroupIdQuery(Guid QuestionGroupId) :
    IMultiProjectionListQuery<QuestionsMultiProjector, GetQuestionsByGroupIdQuery,
        GetQuestionsByGroupIdQuery.ResultRecord>,
    IWaitForSortableUniqueId
{
    public string? WaitForSortableUniqueId { get; init; }
    public int? PageSize { get; init; }
    public int? PageNumber { get; init; }

    public static ResultBox<IEnumerable<ResultRecord>> HandleFilter(
        QuestionsMultiProjector projection,
        GetQuestionsByGroupIdQuery query,
        IQueryContext context)
    {
        return projection.Questions.Values
            .Where(q => q.QuestionGroupId == query.QuestionGroupId)
            .Select(item => new ResultRecord(
                item.QuestionId,
                item.Text,
                item.Options.Select(o => new QuestionOptionRecord(o.Id, o.Text)).ToList(),
                item.IsDisplayed,
                item.QuestionGroupId))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<ResultRecord>> HandleSort(
        IEnumerable<ResultRecord> filteredList,
        GetQuestionsByGroupIdQuery query,
        IQueryContext context) => filteredList.OrderBy(m => m.Id).AsEnumerable().ToResultBox();

    [GenerateSerializer]
    public record ResultRecord(Guid Id, string Text, List<QuestionOptionRecord> Options, bool IsDisplayed, Guid QuestionGroupId);

    [GenerateSerializer]
    public record QuestionOptionRecord(string Id, string Text);
}
