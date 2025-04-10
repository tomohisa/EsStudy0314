using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using Microsoft.AspNetCore.SignalR.Client;

namespace EsCQRSQuestions.AdminWeb;

public class QuestionApiClient(HttpClient httpClient)
{
    // Get all questions
    public async Task<QuestionListQuery.QuestionSummaryRecord[]> GetQuestionsAsync(CancellationToken cancellationToken = default)
    {
        List<QuestionListQuery.QuestionSummaryRecord>? questions = null;

        await foreach (var question in httpClient.GetFromJsonAsAsyncEnumerable<QuestionListQuery.QuestionSummaryRecord>("/api/questions", cancellationToken))
        {
            if (question is not null)
            {
                questions ??= [];
                questions.Add(question);
            }
        }

        return questions?.ToArray() ?? [];
    }

    // Get active question
    public async Task<ActiveQuestionQuery.ActiveQuestionRecord?> GetActiveQuestionAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<ActiveQuestionQuery.ActiveQuestionRecord?>("/api/questions/active", cancellationToken);
    }

    // Get question by ID
    public async Task<QuestionDetailQuery.QuestionDetailRecord?> GetQuestionByIdAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<QuestionDetailQuery.QuestionDetailRecord?>($"/api/questions/{questionId}", cancellationToken);
    }

    // Create question
    public async Task<object> CreateQuestionAsync(string text, List<QuestionOption> options, CancellationToken cancellationToken = default)
    {
        // Use a default question group ID
        var questionGroupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        
        var command = new CreateQuestionCommand(text, options, questionGroupId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/create", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }

    // Create question with specific group ID
    public async Task<object> CreateQuestionWithGroupAsync(string text, List<QuestionOption> options, Guid questionGroupId, CancellationToken cancellationToken = default)
    {
        var command = new CreateQuestionCommand(text, options, questionGroupId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/create", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }

    // Update question
    public async Task<object> UpdateQuestionAsync(Guid questionId, string text, List<QuestionOption> options, CancellationToken cancellationToken = default)
    {
        var command = new UpdateQuestionCommand(questionId, text, options);
        var response = await httpClient.PostAsJsonAsync("/api/questions/update", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }

    // Start displaying a question
    public async Task<object> StartDisplayQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var command = new StartDisplayCommand(questionId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/startDisplay", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }

    // Stop displaying a question
    public async Task<object> StopDisplayQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var command = new StopDisplayCommand(questionId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/stopDisplay", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }

    // Add a response to a question
    public async Task<object> AddResponseAsync(Guid questionId, string? participantName, string selectedOptionId, string? comment, CancellationToken cancellationToken = default)
    {
        var command = new AddResponseCommand(questionId, participantName, selectedOptionId, comment);
        var response = await httpClient.PostAsJsonAsync("/api/questions/addResponse", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }

    // Delete a question
    public async Task<object> DeleteQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var command = new DeleteQuestionCommand(questionId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/delete", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }
}

public static class HubConnectionExtensions
{
    public static IHubConnectionBuilder WithUrlWithClientFactory(this IHubConnectionBuilder builder, string url, IHttpMessageHandlerFactory clientFactory)
    {
        return builder.WithUrl(url, options =>
        {
            options.HttpMessageHandlerFactory = _ => clientFactory.CreateHandler();
        });
    }
}
