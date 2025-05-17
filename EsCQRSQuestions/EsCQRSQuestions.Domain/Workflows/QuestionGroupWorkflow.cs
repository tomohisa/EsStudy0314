using Sekiban.Pure.Executors;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Command.Executor;

namespace EsCQRSQuestions.Domain.Workflows;

public class QuestionGroupWorkflow(ISekibanExecutor executor)
{
    /// <summary>
    /// Command for creating a question group with initial questions
    /// </summary>
    [GenerateSerializer]
    public record CreateGroupWithQuestionsCommand(
        string GroupName,
        List<(string Text, List<QuestionOption> Options)> Questions
    );

    /// <summary>
    /// Command for moving a question between groups
    /// </summary>
    [GenerateSerializer]
    public record MoveQuestionBetweenGroupsCommand(
        Guid QuestionId,
        Guid SourceGroupId,
        Guid TargetGroupId,
        int NewOrder
    );

    /// <summary>
    /// Creates a question and adds it to a group with a specified order
    /// </summary>
    public async Task<ResultBox<CommandResponseSimple>> CreateQuestionAndAddToGroupAsync(
        CreateQuestionCommand command,
        int order)
    {
        // 1. Create the question
        var createQuestionResult = await executor.CommandAsync(command);
        
        // Use Conveyor to process the command result only if it was successful
        return await createQuestionResult.Conveyor(async questionResult => {
            var questionId = questionResult.PartitionKeys.AggregateId;
            
            // 2. Add the question to the group with proper order
            return await executor.CommandAsync(new AddQuestionToGroup(
                command.QuestionGroupId, 
                questionId, 
                order
            )).ToSimpleCommandResponse();
        });
    }

    /// <summary>
    /// Creates a question and adds it to the end of a group
    /// </summary>
    public async Task<ResultBox<CommandResponseSimple>> CreateQuestionAndAddToGroupEndAsync(
        CreateQuestionCommand command)
    {
        // グループ内の質問数を取得して、新しい質問を最後に追加
        var questionsInGroup = await executor.QueryAsync(
            new GetQuestionsByGroupIdQuery(command.QuestionGroupId));
        
        int order = questionsInGroup.IsSuccess ? 
            questionsInGroup.GetValue().Items.Count() : 0;
        
        // 上記のメソッドを使用
        return await CreateQuestionAndAddToGroupAsync(command, order);
    }

    /// <summary>
    /// 重複しないUniqueCodeを生成して検証するワークフロー
    /// </summary>
    public async Task<ResultBox<string>> GenerateUniqueCodeAsync()
    {
        // 6桁のランダムコードを生成
        var uniqueCode = GenerateRandomCode();
        
        // 重複チェック
        var isValid = await ValidateUniqueCodeAsync(uniqueCode);
        
        if (isValid)
        {
            return ResultBox.FromValue(uniqueCode);
        }
        
        // 最大10回まで再試行
        for (int i = 0; i < 10; i++)
        {
            uniqueCode = GenerateRandomCode();
            isValid = await ValidateUniqueCodeAsync(uniqueCode);
            
            if (isValid)
            {
                return ResultBox.FromValue(uniqueCode);
            }
        }
        
        // 10回試行しても重複が解消しない場合はエラー
        return ResultBox.FromException<string>(
            new InvalidOperationException("Failed to generate a unique code after multiple attempts"));
    }

    /// <summary>
    /// 生成されたUniqueCodeが既存のアクティブなQuestionGroupと重複しないことを確認
    /// </summary>
    private async Task<bool> ValidateUniqueCodeAsync(string uniqueCode)
    {
        // 全QuestionGroupを取得
        var groupsResult = await executor.QueryAsync(new GetQuestionGroupsQuery());
        
        if (!groupsResult.IsSuccess)
        {
            return false;
        }
        
        var groups = groupsResult.GetValue();
        
        // 同じUniqueCodeを持つグループがないか確認
        return !groups.Items.Any(g => g.UniqueCode == uniqueCode);
    }

    private static string GenerateRandomCode()
    {
        // 英数字からランダムに6文字を選択
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// UniqueCodeの重複をチェックして新しいQuestionGroupを作成する
    /// </summary>
    public async Task<ResultBox<CommandResponseSimple>> CreateGroupWithUniqueCodeAsync(
        string groupName, 
        string uniqueCode = "")
    {
        // UniqueCodeが指定されていない場合は生成
        if (string.IsNullOrEmpty(uniqueCode))
        {
            var codeResult = await GenerateUniqueCodeAsync();
            if (!codeResult.IsSuccess)
            {
                return codeResult.Exception;
            }
            uniqueCode = codeResult.GetValue();
        }
        else
        {
            // 指定されたUniqueCodeの重複チェック
            var isValid = await ValidateUniqueCodeAsync(uniqueCode);
            if (!isValid)
            {
                return ResultBox.FromException<CommandResponseSimple>(
                    new InvalidOperationException($"UniqueCode '{uniqueCode}' is already in use"));
            }
        }
        
        // グループを作成
        return await executor.CommandAsync(
            new CreateQuestionGroup(groupName, uniqueCode)).ToSimpleCommandResponse();
    }
}
