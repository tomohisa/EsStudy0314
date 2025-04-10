using Sekiban.Pure.Executors;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Workflows;

public class QuestionGroupWorkflow
{
    private readonly ISekibanExecutor _executor;

    public QuestionGroupWorkflow(ISekibanExecutor executor)
    {
        _executor = executor;
    }

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
    /// Creates a group and adds multiple questions at once
    /// </summary>
    public async Task<ResultBox<Guid>> CreateGroupWithQuestionsAsync(CreateGroupWithQuestionsCommand command)
    {
        // 1. Create the question group first
        var groupCommandResult = await _executor.CommandAsync(new CreateQuestionGroup(command.GroupName));
        
        // Use Conveyor to process the command result only if it was successful
        return await groupCommandResult.Conveyor(async groupResult => {
            var groupId = groupResult.PartitionKeys.AggregateId;
            
            // 2. Create each question with the group ID
            var questionTasks = new List<Task<ResultBox<bool>>>();
            int order = 0;
            
            foreach (var (text, options) in command.Questions)
            {
                var task = CreateQuestionAndAddToGroupAsync(text, options, groupId, order++);
                questionTasks.Add(task);
            }
            
            // Wait for all questions to be created and added
            await Task.WhenAll(questionTasks);
            
            // Return the group ID
            return ResultBox.FromValue(groupId);
        });
    }
    
    /// <summary>
    /// Creates a question and adds it to a group
    /// </summary>
    private async Task<ResultBox<bool>> CreateQuestionAndAddToGroupAsync(
        string text, 
        List<QuestionOption> options, 
        Guid groupId, 
        int order)
    {
        // 1. Create the question
        var createQuestionResult = await _executor.CommandAsync(new CreateQuestionCommand(
            text,
            options,
            groupId
        ));
        
        // Use Conveyor to process the command result only if it was successful
        return await createQuestionResult.Conveyor(async questionResult => {
            var questionId = questionResult.PartitionKeys.AggregateId;
            
            // 2. Add the question to the group with proper order
            await _executor.CommandAsync(new AddQuestionToGroup(
                groupId, 
                questionId, 
                order
            ));
            
            return ResultBox.FromValue(true);
        });
    }
    
    /// <summary>
    /// Moves a question from one group to another
    /// </summary>
    public async Task<ResultBox<bool>> MoveQuestionBetweenGroupsAsync(MoveQuestionBetweenGroupsCommand command)
    {
        // 1. Remove from source group
        var removeResult = await _executor.CommandAsync(new RemoveQuestionFromGroup(
            command.SourceGroupId, 
            command.QuestionId
        ));
        
        // Use Conveyor to process the command result only if it was successful
        return await removeResult.Conveyor(async _ => {
            // 2. Add to target group with new order
            await _executor.CommandAsync(new AddQuestionToGroup(
                command.TargetGroupId,
                command.QuestionId,
                command.NewOrder
            ));
            
            // 3. Update the question's group ID
            await _executor.CommandAsync(new UpdateQuestionGroupIdCommand(
                command.QuestionId,
                command.TargetGroupId
            ));
            
            return ResultBox.FromValue(true);
        });
    }
}