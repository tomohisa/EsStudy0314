using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Projections.Questions;
using EsCQRSQuestions.Domain.Extensions; // CommandResponseSimpleを含む名前空間を追加
using Microsoft.AspNetCore.SignalR.Client;

namespace EsCQRSQuestions.AdminWeb;

public class QuestionApiClient(HttpClient httpClient)
{
    // Get all questions (existing method for compatibility)
    public async Task<QuestionListQuery.QuestionSummaryRecord[]> GetQuestionsAsync(string? waitForSortableUniqueId = null, CancellationToken cancellationToken = default)
    {
        List<QuestionListQuery.QuestionSummaryRecord>? questions = null;

        var requestUri = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? "/api/questions"
            : $"/api/questions?waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";

        await foreach (var question in httpClient.GetFromJsonAsAsyncEnumerable<QuestionListQuery.QuestionSummaryRecord>(requestUri, cancellationToken))
        {
            if (question is not null)
            {
                questions ??= [];
                questions.Add(question);
            }
        }

        return questions?.ToArray() ?? [];
    }
    
    // Get all questions with group information using the multi projector
    public async Task<QuestionsQuery.QuestionDetailRecord[]> GetQuestionsWithGroupInfoAsync(string textContains = "", string? waitForSortableUniqueId = null, CancellationToken cancellationToken = default)
    {
        List<QuestionsQuery.QuestionDetailRecord>? questions = null;

        var requestUri = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? $"/api/questions/multi?textContains={Uri.EscapeDataString(textContains ?? "")}"
            : $"/api/questions/multi?textContains={Uri.EscapeDataString(textContains ?? "")}&waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";

        await foreach (var question in httpClient.GetFromJsonAsAsyncEnumerable<QuestionsQuery.QuestionDetailRecord>(requestUri, cancellationToken))
        {
            if (question is not null)
            {
                questions ??= [];
                questions.Add(question);
            }
        }

        return questions?.ToArray() ?? [];
    }
    
    // Get questions by group using the multi projector
    public async Task<QuestionsQuery.QuestionDetailRecord[]> GetQuestionsByGroupAsync(Guid groupId, string textContains = "", string? waitForSortableUniqueId = null, CancellationToken cancellationToken = default)
    {
        List<QuestionsQuery.QuestionDetailRecord>? questions = null;

        var requestUri = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? $"/api/questions/bygroup/{groupId}?textContains={Uri.EscapeDataString(textContains ?? "")}"
            : $"/api/questions/bygroup/{groupId}?textContains={Uri.EscapeDataString(textContains ?? "")}&waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";

        await foreach (var question in httpClient.GetFromJsonAsAsyncEnumerable<QuestionsQuery.QuestionDetailRecord>(requestUri, cancellationToken))
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
    public async Task<ActiveQuestionQuery.ActiveQuestionRecord?> GetActiveQuestionAsync(string? waitForSortableUniqueId = null, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? "/api/questions/active"
            : $"/api/questions/active?waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";
        return await httpClient.GetFromJsonAsync<ActiveQuestionQuery.ActiveQuestionRecord?>(requestUri, cancellationToken);
    }

    // Get question by ID
    public async Task<QuestionDetailQuery.QuestionDetailRecord?> GetQuestionByIdAsync(Guid questionId, string? waitForSortableUniqueId = null, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? $"/api/questions/{questionId}"
            : $"/api/questions/{questionId}?waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";
        return await httpClient.GetFromJsonAsync<QuestionDetailQuery.QuestionDetailRecord?>(requestUri, cancellationToken);
    }

    // Create question
    public async Task<CommandResponseSimple> CreateQuestionAsync(string text, List<QuestionOption> options, CancellationToken cancellationToken = default)
    {
        // Use a default question group ID
        var questionGroupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        
        var command = new CreateQuestionCommand(text, options, questionGroupId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/create", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
    }

    // Create question with specific group ID
    public async Task<CommandResponseSimple> CreateQuestionWithGroupAsync(
        string text, 
        List<QuestionOption> options, 
        Guid questionGroupId, 
        bool allowMultipleResponses = false, // 追加：複数回答フラグ
        CancellationToken cancellationToken = default)
    {
        var command = new CreateQuestionCommand(text, options, questionGroupId, allowMultipleResponses);
        var response = await httpClient.PostAsJsonAsync("/api/questions/create", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
    }

    // Update question
    /// <summary>
    /// 質問を更新します
    /// </summary>
    /// <returns>成功/失敗状態、エラーメッセージ、結果を含むタプル</returns>
    public async Task<(bool Success, string? ErrorMessage, CommandResponseSimple? Result)> UpdateQuestionAsync(
        Guid questionId, 
        string text, 
        List<QuestionOption> options, 
        bool allowMultipleResponses = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new UpdateQuestionCommand(questionId, text, options, allowMultipleResponses);
            var response = await httpClient.PostAsJsonAsync("/api/questions/update", command, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
                           ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
                return (true, null, result);
            }
            else
            {
                // エラー内容を詳細に取得
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return (false, errorContent, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    // Start displaying a question
    public async Task<CommandResponseSimple> StartDisplayQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var command = new StartDisplayCommand(questionId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/startDisplay", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
    }

    // Stop displaying a question
    public async Task<CommandResponseSimple> StopDisplayQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var command = new StopDisplayCommand(questionId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/stopDisplay", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
    }

    // Add a response to a question
    public async Task<CommandResponseSimple> AddResponseAsync(Guid questionId, string? participantName, string selectedOptionId, string? comment, string clientId, CancellationToken cancellationToken = default)
    {
        var command = new AddResponseCommand(questionId, participantName, selectedOptionId, comment, clientId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/addResponse", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
    }

    // Delete a question
    public async Task<CommandResponseSimple> DeleteQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var command = new DeleteQuestionCommand(questionId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/delete", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
              ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
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
