using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using Microsoft.AspNetCore.SignalR.Client;

namespace EsCQRSQuestions.Web;

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
    public async Task<ActiveQuestionQuery.ActiveQuestionRecord?> GetActiveQuestionAsync(string uniqueCode, CancellationToken cancellationToken = default)
    {
        string url = $"/api/questions/active/{uniqueCode}";
        return await httpClient.GetFromJsonAsync<ActiveQuestionQuery.ActiveQuestionRecord?>(url, cancellationToken);
    }

    // Get question by ID
    public async Task<QuestionDetailQuery.QuestionDetailRecord?> GetQuestionByIdAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<QuestionDetailQuery.QuestionDetailRecord?>($"/api/questions/{questionId}", cancellationToken);
    }

    // Add a response to a question
    public async Task<object> AddResponseAsync(Guid questionId, string? participantName, string selectedOptionId, string? comment, string clientId, CancellationToken cancellationToken = default)
    {
        var command = new AddResponseCommand(questionId, participantName, selectedOptionId, comment, clientId);
        var response = await httpClient.PostAsJsonAsync("/api/questions/addResponse", command, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>() ?? new {};
    }

    // 複数回答対応版：Add a response to a question with multiple selected options
    public async Task<object> AddResponseAsync(Guid questionId, string? participantName, List<string> selectedOptionIds, string? comment, string clientId, CancellationToken cancellationToken = default)
    {
        // 現状のAPIは複数回答に直接対応していないため、最初の選択肢を使用して送信
        if (selectedOptionIds == null || !selectedOptionIds.Any())
        {
            throw new ArgumentException("少なくとも1つのオプションを選択してください");
        }

        // 複数の選択肢がある場合は、それぞれに対して個別のリクエストを送信
        List<object> results = new List<object>();
        
        foreach (var optionId in selectedOptionIds)
        {
            var command = new AddResponseCommand(questionId, participantName, optionId, comment, clientId);
            var response = await httpClient.PostAsJsonAsync("/api/questions/addResponse", command, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<object>() ?? new {};
            results.Add(result);
            
            // 最初の回答以外はコメントを空にする（重複を避けるため）
            comment = "";
        }
        
        return results;
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
