using Orleans;
using Sekiban.Pure.Query;
using Sekiban.Pure.Projectors;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

/// <summary>
/// アンケートコードの存在確認を行うクエリ
/// </summary>
[GenerateSerializer]
public record QuestionGroupExistsQuery(string UniqueCode)
    : IMultiProjectionQuery<AggregateListProjector<QuestionGroupProjector>, QuestionGroupExistsQuery, bool>
{
    /// <summary>
    /// クエリ処理を実行します
    /// </summary>
    /// <param name="projectionState">プロジェクション状態</param>
    /// <param name="query">クエリ</param>
    /// <param name="context">クエリコンテキスト</param>
    /// <returns>グループが存在するかどうかを示すブール値</returns>
    public static ResultBox<bool> HandleQuery(
        MultiProjectionState<AggregateListProjector<QuestionGroupProjector>> projectionState,
        QuestionGroupExistsQuery query,
        IQueryContext context)
    {
        // プロジェクションからUniqueCodeが一致するものを探す
        var exists = projectionState.Payload.Aggregates
            .Where(m => m.Value.GetPayload() is QuestionGroup)
            .Select(m => (QuestionGroup)m.Value.GetPayload())
            .Any(g => g.UniqueCode.Equals(query.UniqueCode, StringComparison.OrdinalIgnoreCase));
        
        return ResultBox<bool>.Ok(exists);
    }
}