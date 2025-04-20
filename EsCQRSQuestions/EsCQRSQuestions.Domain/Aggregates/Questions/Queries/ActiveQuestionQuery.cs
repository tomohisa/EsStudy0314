using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Services;
using ResultBoxes;
using Sekiban.Pure.Projectors;
using Sekiban.Pure.Query;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer]
public record ActiveQuestionQuery(Guid QuestionGroupId)
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
            .Where(m => m.Value.Payload is Question)
            .Select(m => new {
                AggregateId = m.Key,
                PartitionKeys = m.Value.PartitionKeys,
                Payload = m.Value.Payload as Question ?? throw new InvalidCastException()
            })
            .Where(m => m.Payload.QuestionGroupId == query.QuestionGroupId)
            .Where(tuple => tuple.Payload.IsDisplayed)
            .ToList();
            
        // 最終的に表示する質問
        // 注：現時点ではUniqueCodeによるフィルタリングを行わない
        // (サービスが静的メソッドでは注入できないため)
        // 実際のフィルタリングはAPIエンドポイント側で行う
        var activeQuestion = questionsWithGroup
            .Select(tuple => new ActiveQuestionRecord(
                tuple.PartitionKeys.AggregateId,
                tuple.Payload.Text,
                tuple.Payload.Options,
                tuple.Payload.Responses.Select(r => new ResponseRecord(
                    r.Id,
                    r.ParticipantName,
                    r.SelectedOptionId,
                    r.Comment,
                    r.Timestamp,
                    r.ClientId)).ToList(),
                tuple.Payload.QuestionGroupId,
                tuple.Payload.AllowMultipleResponses))  // 複数回答フラグを渡す
            .FirstOrDefault();

        return activeQuestion != null 
            ? activeQuestion.ToResultBox() 
            : new ActiveQuestionRecord(
                Guid.Empty, 
                string.Empty, 
                new List<QuestionOption>(), 
                new List<ResponseRecord>(),
                Guid.Empty,
                false).ToResultBox();  // デフォルト値には複数回答フラグもfalseで含める
    }

    [GenerateSerializer]
    public record ActiveQuestionRecord(
        Guid QuestionId,
        string Text,
        List<QuestionOption> Options,
        List<ResponseRecord> Responses,
        Guid QuestionGroupId,  // グループID
        bool AllowMultipleResponses = false  // 追加：複数回答を許可するかどうか
    );

    [GenerateSerializer]
    public record ResponseRecord(
        Guid Id,
        string? ParticipantName,
        string SelectedOptionId,
        string? Comment,
        DateTime Timestamp,
        string ClientId
    );
}
