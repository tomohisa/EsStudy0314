using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using Sekiban.Dcb;

namespace EsCQRSQuestions.Domain.Services;

public class QuestionGroupService(ISekibanExecutor executor)
{
    public async Task<Guid?> GetGroupIdByUniqueCodeAsync(string uniqueCode)
    {
        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            return null;
        }

        var groupsResult = await executor.QueryAsync(new GetQuestionGroupsQuery());
        if (!groupsResult.IsSuccess)
        {
            return null;
        }

        var groups = groupsResult.GetValue();
        var group = groups.Items.FirstOrDefault(g => g.UniqueCode == uniqueCode);
        return group?.Id;
    }

    public async Task<IEnumerable<(Guid Id, string UniqueCode)>> GetAllGroupsAsync()
    {
        var groupsResult = await executor.QueryAsync(new GetQuestionGroupsQuery());
        if (!groupsResult.IsSuccess)
        {
            return Enumerable.Empty<(Guid, string)>();
        }

        var groups = groupsResult.GetValue();
        return groups.Items.Select(g => (g.Id, g.UniqueCode));
    }
}
