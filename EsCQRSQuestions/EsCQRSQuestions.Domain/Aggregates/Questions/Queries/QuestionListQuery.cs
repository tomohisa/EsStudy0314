using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Projections.Questions;
using ResultBoxes;
using Sekiban.Pure.Projectors;
using Sekiban.Pure.Query;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer]
public record QuestionListQuery(string TextContains = "")
    : IMultiProjectionListQuery<AggregateListProjector<QuestionProjector>, QuestionListQuery, QuestionListQuery.QuestionSummaryRecord>
{
    public static ResultBox<IEnumerable<QuestionSummaryRecord>> HandleFilter(
        MultiProjectionState<AggregateListProjector<QuestionProjector>> projection, 
        QuestionListQuery query, 
        IQueryContext context)
    {
        // OrderはMultiProjectorから直接アクセスできないため、デフォルト値を使用
        return projection.Payload.Aggregates
            .Where(m => m.Value.GetPayload() is Question)
            .Select(m => ((Question)m.Value.GetPayload(), m.Value.PartitionKeys))
            .Where(tuple => string.IsNullOrEmpty(query.TextContains) || 
                           tuple.Item1.Text.Contains(query.TextContains, StringComparison.OrdinalIgnoreCase))
            .Select(tuple => new QuestionSummaryRecord(
                tuple.PartitionKeys.AggregateId,
                tuple.Item1.Text,
                tuple.Item1.Options.Count,
                tuple.Item1.IsDisplayed,
                tuple.Item1.Responses.Count))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<QuestionSummaryRecord>> HandleSort(
        IEnumerable<QuestionSummaryRecord> filteredList, 
        QuestionListQuery query, 
        IQueryContext context)
    {
        return filteredList
            .OrderByDescending(m => m.IsDisplayed)
            .ThenBy(m => m.Order)        // Order順を優先
            .ThenBy(m => m.Text)         // 次にテキストで並べ替え
            .AsEnumerable()
            .ToResultBox();
    }

    [GenerateSerializer]
    public record QuestionSummaryRecord(
        Guid QuestionId,
        string Text,
        int OptionCount,
        bool IsDisplayed,
        int ResponseCount,
        int Order = 0 // 表示順序（デフォルト値は0）
    );
}
