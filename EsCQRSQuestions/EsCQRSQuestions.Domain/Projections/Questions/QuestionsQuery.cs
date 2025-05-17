using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Projectors;
using Sekiban.Pure.Query;

namespace EsCQRSQuestions.Domain.Projections.Questions;

[GenerateSerializer]
public record QuestionsQuery(string TextContains = "", Guid? GroupId = null)
    : IMultiProjectionListQuery<QuestionsMultiProjector, QuestionsQuery, QuestionsQuery.QuestionDetailRecord>, IWaitForSortableUniqueId
{
    public static ResultBox<IEnumerable<QuestionDetailRecord>> HandleFilter(
        MultiProjectionState<QuestionsMultiProjector> projection, 
        QuestionsQuery query, 
        IQueryContext context)
    {
        var questions = projection.Payload.Questions.Values;
        
        // フィルタリング: テキスト検索
        if (!string.IsNullOrEmpty(query.TextContains))
        {
            questions = questions.Where(q => 
                q.Text.Contains(query.TextContains, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        
        // フィルタリング: グループIDによる絞り込み
        if (query.GroupId.HasValue)
        {
            questions = questions.Where(q => q.QuestionGroupId == query.GroupId.Value).ToList();
        }
        
        // 結果をマッピング (表示用にOrderに1を足して1始まりにする)
        return questions
            .Select(q => new QuestionDetailRecord(
                q.QuestionId,
                q.Text,
                q.Options,
                q.IsDisplayed,
                q.Responses.Count,
                q.QuestionGroupId,
                q.QuestionGroupName,
                q.Order + 1, // 表示用に+1して1,2,3...となるようにする
                q.AllowMultipleResponses)) // 複数回答フラグを設定
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<QuestionDetailRecord>> HandleSort(
        IEnumerable<QuestionDetailRecord> filteredList, 
        QuestionsQuery query, 
        IQueryContext context)
    {
        return filteredList
            .OrderBy(q => q.QuestionGroupName)
            .ThenBy(q => q.Order)                 // Order順を優先
            .ThenByDescending(q => q.IsDisplayed) // 次に表示状態
            .ThenBy(q => q.Text)                  // 最後にテキスト
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
        bool AllowMultipleResponses = false // 追加：複数回答を許可するかどうか
    );

    public string? WaitForSortableUniqueId { get; set; } = null; 
}
