using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sekiban.Pure.Orleans.Parts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sekiban.Pure.Orleans;

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
        
        // executorを使用してワークフローを直接作成
        var workflow = new QuestionGroupWorkflow(executor);

        try
        {
            _logger.LogInformation("Creating initial question group and questions...");
            
            // 質問のリストを定義
            var questions = new List<(string Text, List<QuestionOption> Options)>
            {
                // Question 1: Event sourcing knowledge
                (
                    "イベントソーシングをどれくらい知っていますか？",
                    new List<QuestionOption>
                    {
                        new("1", "使い込んでいる"),
                        new("2", "使ったことはある"),
                        new("3", "勉強している"),
                        new("4", "これから勉強していきたい"),
                        new("5", "知る必要がない")
                    }
                ),
                // Question 2: Preferred backend language
                (
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
                    }
                ),
                // Question 3: LLM code writing percentage
                (
                    "半年後、何%のコードをLLMに書かせていると思いますか？",
                    new List<QuestionOption>
                    {
                        new("1", "80%以上"),
                        new("2", "50-79%"),
                        new("3", "25-49%"),
                        new("4", "5%-24%"),
                        new("5", "5%未満")
                    }
                ),
                // Question 4: AI coding tools
                (
                    "AIコーディングで一番使っているのは？",
                    new List<QuestionOption>
                    {
                        new("1", "Cline"),
                        new("2", "Cursor"),
                        new("3", "Copilot"),
                        new("4", "Anthropic Code"),
                        new("5", "その他コメントへ"),
                        new("6", "まだ使えていない")
                    }
                )
            };

            // ワークフローを使用してグループと質問を一度に作成
            var command = new QuestionGroupWorkflow.CreateGroupWithQuestionsCommand(
                "初期質問",
                questions
            );
            
            var result = await workflow.CreateGroupWithQuestionsAsync(command);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("Initial question group created with ID: {GroupId}", result.GetValue());
            }
            else
            {
                _logger.LogError("Failed to create initial question group: {Error}", result.GetException().Message);
            }
            
            _logger.LogInformation("Initial questions created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating initial questions");
            throw; // Re-throw to propagate error to the caller
        }
    }

}
