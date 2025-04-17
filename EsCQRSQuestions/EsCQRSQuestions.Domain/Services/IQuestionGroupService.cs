using Sekiban.Pure.Executors;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Services;

/// <summary>
/// グループ情報を扱うサービス
/// </summary>
public class QuestionGroupService
{
    private readonly ISekibanExecutor _executor;
    
    public QuestionGroupService(ISekibanExecutor executor)
    {
        _executor = executor;
    }
    
    /// <summary>
    /// UniqueCodeからグループIDを取得する
    /// </summary>
    public async Task<Guid?> GetGroupIdByUniqueCodeAsync(string uniqueCode)
    {
        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            return null;
        }
        
        var groupsResult = await _executor.QueryAsync(new GetQuestionGroupsQuery());
        if (!groupsResult.IsSuccess)
        {
            return null;
        }
        
        var groups = groupsResult.GetValue();
        var group = groups.Items.FirstOrDefault(g => g.UniqueCode == uniqueCode);
        return group?.Id;
    }
    
    /// <summary>
    /// すべてのグループのID・UniqueCodeのペアを取得する
    /// </summary>
    public async Task<IEnumerable<(Guid Id, string UniqueCode)>> GetAllGroupsAsync()
    {
        var groupsResult = await _executor.QueryAsync(new GetQuestionGroupsQuery());
        if (!groupsResult.IsSuccess)
        {
            return Enumerable.Empty<(Guid, string)>();
        }
        
        var groups = groupsResult.GetValue();
        return groups.Items.Select(g => (g.Id, g.UniqueCode));
    }
}
