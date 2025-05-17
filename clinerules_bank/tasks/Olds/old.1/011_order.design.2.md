# QuestionGroup と Question の表示順序処理の設計

## 現状の問題点

現在、Planning.razor から `CreateQuestions` を実行すると、EsCQRSQuestions/EsCQRSQuestions.ApiService/Program.cs の `/system/createInitialQuestions` エンドポイントが呼び出されています。このエンドポイントは `InitialQuestionsCreator` クラスを使用してQuestionを作成していますが、以下の問題があります：

1. Question のみを作成し、QuestionGroup の Order 設定が行われていない
2. すべての Question が固定の QuestionGroup ID (`11111111-1111-1111-1111-111111111111`) に割り当てられている
3. 順序の概念が適切に実装されていない

一方、EsCQRSQuestions/EsCQRSQuestions.Domain/Workflows/QuestionGroupWorkflow.cs には `CreateGroupWithQuestionsAsync` メソッドが実装されており、以下の機能を持っています：

1. Question グループの作成
2. 複数の Question をグループに追加
3. 各 Question に順序を設定

## 改善計画

### 1. InitialQuestionsCreator の修正

現在の `InitialQuestionsCreator` クラスを修正して `QuestionGroupWorkflow` を利用するようにします。

```csharp
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
        // スコープを使用して必要なサービスを取得
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating initial questions");
            throw; // エラーを呼び出し元に伝播
        }
    }
}
```

### 2. API エンドポイントの修正

`Program.cs` に新しいエンドポイントを追加します。QuestionGroupWorkflow はDIから注入せず、SekibanOrleansExecutor から作成します。

```csharp
// サービス登録は不要 (QuestionGroupWorkflow はDIせず、直接インスタンス化)

// API エンドポイント
apiRoute
    .MapPost(
        "/questionGroups/createWithQuestions",
        async (
            [FromBody] QuestionGroupWorkflow.CreateGroupWithQuestionsCommand command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // executorを使用してワークフローを作成
            var workflow = new QuestionGroupWorkflow(executor);
            var result = await workflow.CreateGroupWithQuestionsAsync(command);
            return result.Match(
                groupId => Results.Ok(new { GroupId = groupId }),
                error => Results.Problem(error.Message)
            );
        })
    .WithOpenApi()
    .WithName("CreateQuestionGroupWithQuestions");
```

### 3. フロントエンドの修正（AdminWeb）

`QuestionGroupApiClient.cs` にワークフローを呼び出すメソッドを追加します。

```csharp
public async Task<Guid> CreateGroupWithQuestionsAsync(
    string groupName,
    List<(string Text, List<QuestionOption> Options)> questions,
    CancellationToken cancellationToken = default)
{
    var command = new CreateGroupWithQuestionsCommand(groupName, questions);
    var response = await _httpClient.PostAsJsonAsync("/api/questionGroups/createWithQuestions", command, cancellationToken);
    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken);
    return result.GroupId;
}
```

## 実装手順

1. `InitialQuestionsCreator.cs` を修正して `QuestionGroupWorkflow` を利用するよう変更
2. `Program.cs` に新しいエンドポイント追加 (DIへの登録は不要)
3. `QuestionGroupApiClient.cs` に新しいメソッドを追加
4. （必要に応じて）Planning.razor の実装を更新

## 利点

1. 質問グループと質問の作成を一度のトランザクションで行える
2. 質問の順序が適切に設定される
3. API 呼び出し回数が減少する（複数のコマンドが単一のワークフローに集約される）
4. コードがより再利用可能になる

## 注意点

1. 既存の機能と互換性を保持すること
2. エラーハンドリングを適切に行うこと
3. ワークフローの呼び出しに失敗した場合の回復メカニズムを検討すること
