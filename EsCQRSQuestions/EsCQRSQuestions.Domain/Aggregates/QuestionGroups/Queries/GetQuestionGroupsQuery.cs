using Sekiban.Pure.Query;
using Sekiban.Pure.Projectors;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;

[GenerateSerializer]
public record GetQuestionGroupsQuery() : 
    IMultiProjectionListQuery<AggregateListProjector<QuestionGroupProjector>, GetQuestionGroupsQuery, GetQuestionGroupsQuery.ResultRecord>,
    IWaitForSortableUniqueId
{
    /// <summary>
    /// 指定されたイベントが処理されるまで待機するためのソータブルユニークID
    /// </summary>
    public string? WaitForSortableUniqueId { get; set; }

    public static ResultBox<IEnumerable<ResultRecord>> HandleFilter(
        MultiProjectionState<AggregateListProjector<QuestionGroupProjector>> projection, 
        GetQuestionGroupsQuery query, 
        IQueryContext context)
    {
        return projection.Payload.Aggregates
            // より明示的にEmptyAggregatePayloadを除外するフィルタリング
            .Where(m => m.Value.GetPayload() is QuestionGroup)
            .Select(m => ((QuestionGroup)m.Value.GetPayload(), m.Value.PartitionKeys))
            .Select(tuple => new ResultRecord(
                tuple.PartitionKeys.AggregateId, 
                tuple.Item1.Name,
                tuple.Item1.UniqueCode, // 新規：UniqueCodeを追加
                tuple.Item1.Questions.Select(q => new QuestionReferenceRecord(q.QuestionId, q.Order)).ToList()))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<ResultRecord>> HandleSort(
        IEnumerable<ResultRecord> filteredList, 
        GetQuestionGroupsQuery query, 
        IQueryContext context)
    {
        // 名前でソート
        return filteredList.OrderBy(m => m.Name).AsEnumerable().ToResultBox();
    }

    [GenerateSerializer]
    public record ResultRecord(
        Guid Id, 
        string Name,
        string UniqueCode, // 新規：UniqueCodeを追加
        List<QuestionReferenceRecord> Questions
    );

    [GenerateSerializer]
    public record QuestionReferenceRecord(
        Guid QuestionId, 
        int Order
    );
}
