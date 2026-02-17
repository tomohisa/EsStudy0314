using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Workflows;
using EsCQRSQuestions.Domain.Projections.Questions;
using Sekiban.Pure.Command.Executor;

namespace EsCQRSQuestions.AdminWeb;

public class QuestionGroupApiClient(HttpClient httpClient)
{
    private static bool IsTransient(HttpStatusCode statusCode, string? content) =>
        statusCode == HttpStatusCode.InternalServerError
        || (content?.Contains("DbUpdateException", StringComparison.OrdinalIgnoreCase) ?? false)
        || (content?.Contains("Stream type mismatch", StringComparison.OrdinalIgnoreCase) ?? false);

    private static TimeSpan Backoff(int attempt) => TimeSpan.FromMilliseconds(150 * attempt * attempt);

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken,
        int maxAttempts = 6)
    {
        for (var attempt = 1; ; attempt++)
        {
            var response = await send();
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (attempt >= maxAttempts || !IsTransient(response.StatusCode, content))
            {
                throw new HttpRequestException($"API request failed with status {response.StatusCode}: {content}");
            }

            await Task.Delay(Backoff(attempt), cancellationToken);
        }
    }

    // Get all question groups
    public async Task<List<GetQuestionGroupsQuery.ResultRecord>> GetGroupsAsync(
        string? waitForSortableUniqueId = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? "/api/questionGroups"
            : $"/api/questionGroups?waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";

        var response = await SendWithRetryAsync(
            () => httpClient.GetAsync(requestUri, cancellationToken),
            cancellationToken);
        var groups = await response.Content.ReadFromJsonAsync<List<GetQuestionGroupsQuery.ResultRecord>>(cancellationToken);
        
        return groups ?? new List<GetQuestionGroupsQuery.ResultRecord>();
    }
    
    // Get a specific question group
    public async Task<GetQuestionGroupsQuery.ResultRecord?> GetGroupAsync(
        Guid groupId,
        string? waitForSortableUniqueId = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? $"/api/questionGroups/{groupId}"
            : $"/api/questionGroups/{groupId}?waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";
            
        return await httpClient.GetFromJsonAsync<GetQuestionGroupsQuery.ResultRecord?>(
            requestUri, 
            cancellationToken);
    }
    
    // Get questions in a group
    public async Task<List<QuestionsQuery.QuestionDetailRecord>> GetQuestionsInGroupAsync(
        Guid groupId,
        string? waitForSortableUniqueId = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? $"/api/questionGroups/{groupId}/questions"
            : $"/api/questionGroups/{groupId}/questions?waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";
            
        var questions = await httpClient.GetFromJsonAsync<List<QuestionsQuery.QuestionDetailRecord>>(
            requestUri, 
            cancellationToken);
        
        return questions ?? new List<QuestionsQuery.QuestionDetailRecord>();
    }
    
    // Create a new question group
    public async Task<CommandResponseSimple> CreateGroupAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new CreateQuestionGroup(name);
            var response = await SendWithRetryAsync(
                () => httpClient.PostAsJsonAsync("/api/questionGroups", command, cancellationToken),
                cancellationToken);
            return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
                  ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
        }
        catch (HttpRequestException)
        {
            throw; // Re-throw HTTP exceptions as-is
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create group: {ex.Message}", ex);
        }
    }
    
    // Update a question group's name
    public async Task<CommandResponseSimple> UpdateGroupAsync(
        Guid groupId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new UpdateQuestionGroupCommand(groupId, newName);
            var response = await SendWithRetryAsync(
                () => httpClient.PutAsJsonAsync($"/api/questionGroups/{groupId}", command, cancellationToken),
                cancellationToken);
            return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
                  ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
        }
        catch (HttpRequestException)
        {
            throw; // Re-throw HTTP exceptions as-is
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to update group: {ex.Message}", ex);
        }
    }
    
    // Delete a question group
    public async Task<CommandResponseSimple> DeleteGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendWithRetryAsync(
                () => httpClient.DeleteAsync($"/api/questionGroups/{groupId}", cancellationToken),
                cancellationToken);
            return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
                  ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
        }
        catch (HttpRequestException)
        {
            throw; // Re-throw HTTP exceptions as-is
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete group: {ex.Message}", ex);
        }
    }
    
    // Add a question to a group
    public async Task<CommandResponseSimple> AddQuestionToGroupAsync(
        Guid groupId,
        Guid questionId,
        int order,
        CancellationToken cancellationToken = default)
    {
        var command = new AddQuestionToGroup(groupId, questionId, order);
        var response = await httpClient.PostAsJsonAsync($"/api/questionGroups/{groupId}/questions", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
    }
    
    // Change a question's order within a group
    public async Task<CommandResponseSimple> ChangeQuestionOrderAsync(
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
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
    }
    
    // Remove a question from a group
    public async Task<CommandResponseSimple> RemoveQuestionFromGroupAsync(
        Guid groupId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/questionGroups/{groupId}/questions/{questionId}", 
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
    }
    
    // Move a question between groups
    public async Task<CommandResponseSimple> MoveQuestionBetweenGroupsAsync(
        QuestionGroupWorkflow.MoveQuestionBetweenGroupsCommand command,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            "/workflows/questionGroups/moveQuestion", 
            command, 
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
    }
}
