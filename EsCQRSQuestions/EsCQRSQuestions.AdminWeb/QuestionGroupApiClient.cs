using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Workflows;

namespace EsCQRSQuestions.AdminWeb;

public class QuestionGroupApiClient(HttpClient httpClient)
{
    // Get all question groups
    public async Task<List<GetQuestionGroupsQuery.ResultRecord>> GetGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        var groups = await httpClient.GetFromJsonAsync<List<GetQuestionGroupsQuery.ResultRecord>>(
            "/api/questionGroups", 
            cancellationToken);
        
        return groups ?? new List<GetQuestionGroupsQuery.ResultRecord>();
    }
    
    // Get a specific question group
    public async Task<GetQuestionGroupsQuery.ResultRecord?> GetGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<GetQuestionGroupsQuery.ResultRecord?>(
            $"/api/questionGroups/{groupId}", 
            cancellationToken);
    }
    
    // Get questions in a group
    public async Task<List<GetQuestionsByGroupIdQuery.ResultRecord>> GetQuestionsInGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var questions = await httpClient.GetFromJsonAsync<List<GetQuestionsByGroupIdQuery.ResultRecord>>(
            $"/api/questionGroups/{groupId}/questions", 
            cancellationToken);
        
        return questions ?? new List<GetQuestionsByGroupIdQuery.ResultRecord>();
    }
    
    // Create a new question group
    public async Task<object> CreateGroupAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateQuestionGroup(name);
        var response = await httpClient.PostAsJsonAsync("/api/questionGroups", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }
    
    // Update a question group's name
    public async Task<object> UpdateGroupAsync(
        Guid groupId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateQuestionGroupName(groupId, newName);
        var response = await httpClient.PutAsJsonAsync($"/api/questionGroups/{groupId}", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }
    
    // Delete a question group
    public async Task<object> DeleteGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/questionGroups/{groupId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }
    
    // Add a question to a group
    public async Task<object> AddQuestionToGroupAsync(
        Guid groupId,
        Guid questionId,
        int order,
        CancellationToken cancellationToken = default)
    {
        var command = new AddQuestionToGroup(groupId, questionId, order);
        var response = await httpClient.PostAsJsonAsync($"/api/questionGroups/{groupId}/questions", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }
    
    // Change a question's order within a group
    public async Task<object> ChangeQuestionOrderAsync(
        Guid groupId,
        Guid questionId,
        int newOrder,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/questionGroups/{groupId}/questions/{questionId}/order", 
            newOrder, 
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }
    
    // Remove a question from a group
    public async Task<object> RemoveQuestionFromGroupAsync(
        Guid groupId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/questionGroups/{groupId}/questions/{questionId}", 
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }
    
    // Move a question between groups
    public async Task<object> MoveQuestionBetweenGroupsAsync(
        QuestionGroupWorkflow.MoveQuestionBetweenGroupsCommand command,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            "/workflows/questionGroups/moveQuestion", 
            command, 
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }
}