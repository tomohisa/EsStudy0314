using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Workflows;
using EsCQRSQuestions.Domain.Projections.Questions;

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
    public async Task<List<QuestionsQuery.QuestionDetailRecord>> GetQuestionsInGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var questions = await httpClient.GetFromJsonAsync<List<QuestionsQuery.QuestionDetailRecord>>(
            $"/api/questions/bygroup/{groupId}", 
            cancellationToken);
        
        return questions ?? new List<QuestionsQuery.QuestionDetailRecord>();
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
    
    // Create a group with questions
    public async Task<Guid> CreateGroupWithQuestionsAsync(
        string groupName,
        List<(string Text, List<QuestionOption> Options)> questions,
        CancellationToken cancellationToken = default)
    {
        var command = new QuestionGroupWorkflow.CreateGroupWithQuestionsCommand(groupName, questions);
        var response = await httpClient.PostAsJsonAsync("/api/questionGroups/createWithQuestions", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken);
        return result?.GroupId;
    }
}
