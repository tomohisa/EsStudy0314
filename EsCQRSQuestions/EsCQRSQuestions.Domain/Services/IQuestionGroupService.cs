using Sekiban.Pure.Orleans.Parts;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Services;

/// <summary>
/// グループ情報を扱うサービスインターフェース
/// </summary>
public interface IQuestionGroupService
{
    /// <summary>
    /// UniqueCodeからグループIDを取得する
    /// </summary>
    Task<Guid?> GetGroupIdByUniqueCodeAsync(string uniqueCode);
    
    /// <summary>
    /// すべてのグループのID・UniqueCodeのペアを取得する
    /// </summary>
    Task<IEnumerable<(Guid Id, string UniqueCode)>> GetAllGroupsAsync();
}

/// <summary>
/// グループ情報を扱うサービスの実装
/// </summary>
public class QuestionGroupService : IQuestionGroupService
{
    private readonly SekibanOrleansExecutor _executor;
    
    public QuestionGroupService(SekibanOrleansExecutor executor)
    {
        _executor = executor;
    }
    
    /// <inheritdoc/>
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
    
    /// <inheritdoc/>
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
