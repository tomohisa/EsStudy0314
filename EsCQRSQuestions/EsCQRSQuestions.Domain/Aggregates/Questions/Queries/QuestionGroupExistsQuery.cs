using Orleans;
using Sekiban.Core.Query.MultiProjections;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

/// <summary>
/// アンケートコードの存在確認を行うクエリ
/// </summary>
[GenerateSerializer]
public record QuestionGroupExistsQuery(string UniqueCode)
    : IMultiProjectionQuery<QuestionGroupListProjection, QuestionGroupExistsQuery, bool>
{
    /// <summary>
    /// クエリ処理を実行します
    /// </summary>
    /// <param name="projectionState">プロジェクション状態</param>
    /// <param name="query">クエリ</param>
    /// <param name="context">クエリコンテキスト</param>
    /// <returns>グループが存在するかどうかを示すブール値</returns>
    public static ResultBoxes.ResultBox<bool> HandleQuery(
        MultiProjectionState<QuestionGroupListProjection> projectionState,
        QuestionGroupExistsQuery query,
        IQueryContext context)
    {
        // プロジェクションからUniqueCodeが一致するものを探す
        var exists = projectionState.Payload.QuestionGroups
            .Any(g => g.UniqueCode.Equals(query.UniqueCode, StringComparison.OrdinalIgnoreCase));
        
        return ResultBoxes.ResultBox<bool>.Ok(exists);
    }
}