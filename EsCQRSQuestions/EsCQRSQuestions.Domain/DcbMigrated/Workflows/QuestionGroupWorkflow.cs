using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Dcb;
using Sekiban.Pure.Command.Executor;

namespace EsCQRSQuestions.Domain.Workflows;

public class QuestionGroupWorkflow(ISekibanExecutor executor)
{
    [GenerateSerializer]
    public record CreateGroupWithQuestionsCommand(string GroupName, List<(string Text, List<QuestionOption> Options)> Questions);

    [GenerateSerializer]
    public record MoveQuestionBetweenGroupsCommand(Guid QuestionId, Guid SourceGroupId, Guid TargetGroupId, int NewOrder);

    public async Task<ResultBox<CommandResponseSimple>> CreateQuestionAndAddToGroupAsync(CreateQuestionCommand command,
        int order)
    {
        var createQuestionResult = await executor.ExecuteAsync(command);
        return await createQuestionResult.Conveyor(async questionResult =>
        {
            var questionId = questionResult.Events
                .Select(e => e.Payload)
                .OfType<EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionCreated>()
                .Select(e => e.QuestionId)
                .FirstOrDefault();
            return await executor.ExecuteAsync(new AddQuestionToGroup(command.QuestionGroupId, questionId, order))
                .ToSimpleCommandResponse();
        });
    }

    public async Task<ResultBox<CommandResponseSimple>> CreateQuestionAndAddToGroupEndAsync(CreateQuestionCommand command)
    {
        var questionsInGroup = await executor.QueryAsync(new GetQuestionsByGroupIdQuery(command.QuestionGroupId));
        var order = questionsInGroup.IsSuccess ? questionsInGroup.GetValue().Items.Count() : 0;
        return await CreateQuestionAndAddToGroupAsync(command, order);
    }

    public async Task<ResultBox<string>> GenerateUniqueCodeAsync()
    {
        var uniqueCode = GenerateRandomCode();
        var isValid = await ValidateUniqueCodeAsync(uniqueCode);
        if (isValid)
        {
            return ResultBox.FromValue(uniqueCode);
        }

        for (var i = 0; i < 10; i++)
        {
            uniqueCode = GenerateRandomCode();
            isValid = await ValidateUniqueCodeAsync(uniqueCode);
            if (isValid)
            {
                return ResultBox.FromValue(uniqueCode);
            }
        }

        return ResultBox.FromException<string>(new InvalidOperationException(
            "Failed to generate a unique code after multiple attempts"));
    }

    private async Task<bool> ValidateUniqueCodeAsync(string uniqueCode)
    {
        var groupsResult = await executor.QueryAsync(new GetQuestionGroupsQuery());
        if (!groupsResult.IsSuccess)
        {
            return false;
        }

        var groups = groupsResult.GetValue();
        return !groups.Items.Any(g => g.UniqueCode == uniqueCode);
    }

    private static string GenerateRandomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public async Task<ResultBox<CommandResponseSimple>> CreateGroupWithUniqueCodeAsync(string groupName,
        string uniqueCode = "")
    {
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
            var isValid = await ValidateUniqueCodeAsync(uniqueCode);
            if (!isValid)
            {
                return ResultBox.FromException<CommandResponseSimple>(
                    new InvalidOperationException($"UniqueCode '{uniqueCode}' is already in use"));
            }
        }

        return await executor.ExecuteAsync(new CreateQuestionGroup(groupName, uniqueCode)).ToSimpleCommandResponse();
    }
}
