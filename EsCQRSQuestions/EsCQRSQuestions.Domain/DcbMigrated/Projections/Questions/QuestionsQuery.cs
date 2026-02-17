using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Projections.Questions;

[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public record QuestionsQuery(string TextContains = "", Guid? GroupId = null)
    : IMultiProjectionListQuery<QuestionsMultiProjector, QuestionsQuery, QuestionsQuery.QuestionDetailRecord>,
        IWaitForSortableUniqueId
{
    public string? WaitForSortableUniqueId { get; init; }
    public int? PageSize { get; init; }
    public int? PageNumber { get; init; }

    public static ResultBox<IEnumerable<QuestionDetailRecord>> HandleFilter(
        QuestionsMultiProjector projection,
        QuestionsQuery query,
        IQueryContext context)
    {
        var questions = projection.Questions.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(query.TextContains))
        {
            questions = questions.Where(q => q.Text.Contains(query.TextContains, StringComparison.OrdinalIgnoreCase));
        }

        if (query.GroupId.HasValue)
        {
            questions = questions.Where(q => q.QuestionGroupId == query.GroupId.Value);
        }

        return questions.Select(q => new QuestionDetailRecord(
                q.QuestionId,
                q.Text,
                q.Options,
                q.IsDisplayed,
                q.Responses.Count,
                q.QuestionGroupId,
                q.QuestionGroupName,
                q.Order + 1,
                q.AllowMultipleResponses))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<QuestionDetailRecord>> HandleSort(
        IEnumerable<QuestionDetailRecord> filteredList,
        QuestionsQuery query,
        IQueryContext context)
    {
        return filteredList
            .OrderBy(q => q.QuestionGroupName)
            .ThenBy(q => q.Order)
            .ThenByDescending(q => q.IsDisplayed)
            .ThenBy(q => q.Text)
            .AsEnumerable()
            .ToResultBox();
    }

    [GenerateSerializer]
    public record QuestionDetailRecord(
        Guid QuestionId,
        string Text,
        List<QuestionOption> Options,
        bool IsDisplayed,
        int ResponseCount,
        Guid QuestionGroupId,
        string QuestionGroupName,
        int Order,
        bool AllowMultipleResponses = false);
}
