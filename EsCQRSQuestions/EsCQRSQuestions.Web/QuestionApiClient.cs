using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Extensions; // CommandResponseSimpleを含む名前空間を追加
using Microsoft.AspNetCore.SignalR.Client;

namespace EsCQRSQuestions.Web;

public class QuestionApiClient(HttpClient httpClient)
{
    // Get all questions
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

    // Get active question
    public async Task<ActiveQuestionQuery.ActiveQuestionRecord?> GetActiveQuestionAsync(string uniqueCode, string? waitForSortableUniqueId = null, CancellationToken cancellationToken = default)
    {
        string url = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? $"/api/questions/active/{uniqueCode}"
            : $"/api/questions/active/{uniqueCode}?waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";
        return await httpClient.GetFromJsonAsync<ActiveQuestionQuery.ActiveQuestionRecord?>(url, cancellationToken);
    }

    /// <summary>
    /// アンケートコードの有効性を検証します
    /// </summary>
    /// <param name="uniqueCode">検証するアンケートコード</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>コードが有効な場合はtrue、それ以外はfalse</returns>
    public async Task<bool> ValidateUniqueCodeAsync(string uniqueCode, CancellationToken cancellationToken = default)
    {
        try
        {
            string url = $"/api/questions/validate/{uniqueCode}";
            var response = await httpClient.GetAsync(url, cancellationToken);
            
            // ステータスコードで判断（404の場合はコードが存在しない）
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // 例外が発生した場合は無効と判断
            return false;
        }
    }

    // Get question by ID
    public async Task<QuestionDetailQuery.QuestionDetailRecord?> GetQuestionByIdAsync(Guid questionId, string? waitForSortableUniqueId = null, CancellationToken cancellationToken = default)
    {
        string url = string.IsNullOrEmpty(waitForSortableUniqueId)
            ? $"/api/questions/{questionId}"
            : $"/api/questions/{questionId}?waitForSortableUniqueId={Uri.EscapeDataString(waitForSortableUniqueId)}";
        return await httpClient.GetFromJsonAsync<QuestionDetailQuery.QuestionDetailRecord?>(url, cancellationToken);
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

    // 複数回答対応版：Add a response to a question with multiple selected options
    public async Task<List<CommandResponseSimple>> AddResponseAsync(Guid questionId, string? participantName, List<string> selectedOptionIds, string? comment, string clientId, CancellationToken cancellationToken = default)
    {
        // 現状のAPIは複数回答に直接対応していないため、最初の選択肢を使用して送信
        if (selectedOptionIds == null || !selectedOptionIds.Any())
        {
            throw new ArgumentException("少なくとも1つのオプションを選択してください");
        }

        // 複数の選択肢がある場合は、それぞれに対して個別のリクエストを送信
        List<CommandResponseSimple> results = new List<CommandResponseSimple>();
        
        foreach (var optionId in selectedOptionIds)
        {
            var command = new AddResponseCommand(questionId, participantName, optionId, comment, clientId);
            var response = await httpClient.PostAsJsonAsync("/api/questions/addResponse", command, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CommandResponseSimple>(cancellationToken) 
                        ?? throw new InvalidOperationException("Failed to deserialize CommandResponse");
            results.Add(result);
            
            // 最初の回答以外はコメントを空にする（重複を避けるため）
            comment = "";
        }
        
        return results;
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
