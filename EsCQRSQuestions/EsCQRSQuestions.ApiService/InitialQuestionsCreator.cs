using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sekiban.Pure.Orleans.Parts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EsCQRSQuestions.ApiService;

public class InitialQuestionsCreator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InitialQuestionsCreator> _logger;

    public InitialQuestionsCreator(
        IServiceProvider serviceProvider,
        ILogger<InitialQuestionsCreator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task CreateInitialQuestions(CancellationToken cancellationToken = default)
    {
        // Use a scope to get the required services
        using var scope = _serviceProvider.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<SekibanOrleansExecutor>();

        try
        {
            _logger.LogInformation("Creating initial questions...");
            
            // Question 1: Event sourcing knowledge
            await CreateQuestionIfNotExists(
                executor,
                "イベントソーシングをどれくらい知っていますか？",
                new List<QuestionOption>
                {
                    new("1", "使い込んでいる"),
                    new("2", "使ったことはある"),
                    new("3", "勉強している"),
                    new("4", "これから勉強していきたい"),
                    new("5", "知る必要がない")
                },
                cancellationToken);

            // Question 2: Preferred backend language
            await CreateQuestionIfNotExists(
                executor,
                "バックエンドの言語で一番得意なものはなんですか？",
                new List<QuestionOption>
                {
                    new("1", "Typescript"),
                    new("2", "Rust"),
                    new("3", "Go"),
                    new("4", "C#"),
                    new("5", "Ruby"),
                    new("6", "PHP"),
                    new("7", "java"),
                    new("8", "その他コメントへ")
                },
                cancellationToken);

            // Question 3: LLM code writing percentage
            await CreateQuestionIfNotExists(
                executor,
                "半年後、何%のコードをLLMに書かせていると思いますか？",
                new List<QuestionOption>
                {
                    new("1", "80%以上"),
                    new("2", "50-79%"),
                    new("3", "25-49%"),
                    new("4", "5%-24%"),
                    new("5", "5%未満")
                },
                cancellationToken);

            // Question 4: AI coding tools
            await CreateQuestionIfNotExists(
                executor,
                "AIコーディングで一番使っているのは？",
                new List<QuestionOption>
                {
                    new("1", "Cline"),
                    new("2", "Cursor"),
                    new("3", "Copilot"),
                    new("4", "Anthropic Code"),
                    new("5", "その他コメントへ"),
                    new("6", "まだ使えていない")
                },
                cancellationToken);

            _logger.LogInformation("Initial questions created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating initial questions");
            throw; // Re-throw to propagate error to the caller
        }
    }

    private async Task CreateQuestionIfNotExists(
        SekibanOrleansExecutor executor,
        string text,
        List<QuestionOption> options,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        int retryCount = 0;
        
        while (retryCount < maxRetries)
        {
            try
            {
                // Create a default question group ID
                var questionGroupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
                
                // Create the question with the required QuestionGroupId parameter
                var command = new CreateQuestionCommand(text, options, questionGroupId);
                await executor.CommandAsync(command);
                _logger.LogInformation("Created question: {Text}", text);
                return; // Success, exit the method
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    _logger.LogWarning(ex, "Failed to create question after {RetryCount} attempts: {Text}. It might already exist.", 
                        retryCount, text);
                }
                else
                {
                    _logger.LogWarning("Attempt {RetryCount} failed to create question: {Text}. Retrying in 2 seconds...", 
                        retryCount, text);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
        }
    }
}
