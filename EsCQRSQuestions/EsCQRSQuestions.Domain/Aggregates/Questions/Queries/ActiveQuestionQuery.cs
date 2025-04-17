using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Services;
using ResultBoxes;
using Sekiban.Pure.Projectors;
using Sekiban.Pure.Query;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer]
public record ActiveQuestionQuery(string? UniqueCode = null)
    : IMultiProjectionQuery<AggregateListProjector<QuestionProjector>, ActiveQuestionQuery, ActiveQuestionQuery.ActiveQuestionRecord>
{
    /// <summary>
    /// 質問プロジェクションからアクティブな質問を取得するクエリハンドラー
    /// UniqueCodeが指定されている場合は、そのグループに関連する質問のみを返す
    /// </summary>
    public static ResultBox<ActiveQuestionRecord> HandleQuery(
        MultiProjectionState<AggregateListProjector<QuestionProjector>> projection,
        ActiveQuestionQuery query,
        IQueryContext context)
    {
        // 質問のリストを取得
        var questionsWithGroup = projection.Payload.Aggregates
            .Where(m => m.Value.GetPayload() is Question)
            .Select(m => ((Question)m.Value.GetPayload(), m.Value.PartitionKeys))
            .Where(tuple => tuple.Item1.IsDisplayed)
            .ToList();
            
        // 最終的に表示する質問
        // 注：現時点ではUniqueCodeによるフィルタリングを行わない
        // (サービスが静的メソッドでは注入できないため)
        // 実際のフィルタリングはAPIエンドポイント側で行う
        var activeQuestion = questionsWithGroup
            .Select(tuple => new ActiveQuestionRecord(
                tuple.PartitionKeys.AggregateId,
                tuple.Item1.Text,
                tuple.Item1.Options,
                tuple.Item1.Responses.Select(r => new ResponseRecord(
                    r.Id,
                    r.ParticipantName,
                    r.SelectedOptionId,
                    r.Comment,
                    r.Timestamp)).ToList(),
                tuple.Item1.QuestionGroupId))  // グループIDも返す
            .FirstOrDefault();

        return activeQuestion != null 
            ? activeQuestion.ToResultBox() 
            : new ActiveQuestionRecord(
                Guid.Empty, 
                string.Empty, 
                new List<QuestionOption>(), 
                new List<ResponseRecord>(),
                Guid.Empty).ToResultBox();  // デフォルト値にもグループIDを含める
    }

    [GenerateSerializer]
    public record ActiveQuestionRecord(
        Guid QuestionId,
        string Text,
        List<QuestionOption> Options,
        List<ResponseRecord> Responses,
        Guid QuestionGroupId  // 追加：グループID
    );

    [GenerateSerializer]
    public record ResponseRecord(
        Guid Id,
        string? ParticipantName,
        string SelectedOptionId,
        string? Comment,
        DateTime Timestamp
    );
}
