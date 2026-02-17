using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public record QuestionListQuery(string TextContains = "") :
    IMultiProjectionListQuery<GenericTagMultiProjector<QuestionProjector, QuestionTag>, QuestionListQuery,
        QuestionListQuery.QuestionSummaryRecord>
{
    public int? PageSize { get; init; }
    public int? PageNumber { get; init; }

    public static ResultBox<IEnumerable<QuestionSummaryRecord>> HandleFilter(
        GenericTagMultiProjector<QuestionProjector, QuestionTag> projection,
        QuestionListQuery query,
        IQueryContext context)
    {
        return projection.GetCurrentTagStates().Values
            .Where(m => m.Payload is Question)
            .Select(m => (Question)m.Payload)
            .Where(item => string.IsNullOrEmpty(query.TextContains) ||
                           item.Text.Contains(query.TextContains, StringComparison.OrdinalIgnoreCase))
            .Select(item => new QuestionSummaryRecord(
                item.QuestionId,
                item.Text,
                item.Options.Count,
                item.IsDisplayed,
                item.Responses.Count))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<QuestionSummaryRecord>> HandleSort(
        IEnumerable<QuestionSummaryRecord> filteredList,
        QuestionListQuery query,
        IQueryContext context)
    {
        return filteredList
            .OrderByDescending(m => m.IsDisplayed)
            .ThenBy(m => m.Order)
            .ThenBy(m => m.Text)
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
        int Order = 0);
}
