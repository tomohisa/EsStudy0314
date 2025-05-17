using Sekiban.Pure.Query;
using Sekiban.Pure.Projectors;
using EsCQRSQuestions.Domain.Aggregates.Questions;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;

[GenerateSerializer]
public record GetQuestionsByGroupIdQuery(Guid QuestionGroupId) : 
    IMultiProjectionListQuery<AggregateListProjector<QuestionProjector>, GetQuestionsByGroupIdQuery, GetQuestionsByGroupIdQuery.ResultRecord>,
    IWaitForSortableUniqueId
{
    /// <summary>
    /// 指定されたイベントが処理されるまで待機するためのソータブルユニークID
    /// </summary>
    public string? WaitForSortableUniqueId { get; set; }

    public static ResultBox<IEnumerable<ResultRecord>> HandleFilter(
        MultiProjectionState<AggregateListProjector<QuestionProjector>> projection, 
        GetQuestionsByGroupIdQuery query, 
        IQueryContext context)
    {
        return projection.Payload.Aggregates
            .Where(m => m.Value.GetPayload() is Question q && q.QuestionGroupId == query.QuestionGroupId)
            .Select(m => ((Question)m.Value.GetPayload(), m.Value.PartitionKeys))
            .Select(tuple => new ResultRecord(
                tuple.PartitionKeys.AggregateId, 
                tuple.Item1.Text, 
                tuple.Item1.Options.Select(o => new QuestionOptionRecord(o.Id, o.Text)).ToList(),
                tuple.Item1.IsDisplayed,
                tuple.Item1.QuestionGroupId))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<ResultRecord>> HandleSort(
        IEnumerable<ResultRecord> filteredList, 
        GetQuestionsByGroupIdQuery query, 
        IQueryContext context)
    {
        // For now, just sort by ID since we can't directly access the executor from here
        // In a real implementation, we would need to inject the ISekibanExecutor or handle this differently
        return filteredList.OrderBy(m => m.Id).AsEnumerable().ToResultBox();
    }

    [GenerateSerializer]
    public record ResultRecord(
        Guid Id, 
        string Text, 
        List<QuestionOptionRecord> Options,
        bool IsDisplayed,
        Guid QuestionGroupId
    );

    [GenerateSerializer]
    public record QuestionOptionRecord(
        string Id, 
        string Text
    );
}