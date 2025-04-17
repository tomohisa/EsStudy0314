# Question作成時のQuestionGroupとOrderの処理設計

## 現状の問題点

現在、`/api/questions/create` エンドポイントを使用してQuestionを作成する際、以下の問題があります：

1. QuestionはCreateQuestionCommandで作成されますが、QuestionGroupへの追加が自動的に行われていない
2. QuestionGroupに追加される際に順序（Order）が適切に設定されていない
3. 既に実装したQuestionGroupWorkflowを活用していない

```csharp
// 現在のCreateQuestionCommandの定義
[GenerateSerializer]
public record CreateQuestionCommand(
    string Text,
    List<QuestionOption> Options,
    Guid QuestionGroupId
) : ICommandWithHandler<CreateQuestionCommand, QuestionProjector>
{
    // 実装省略
}
```

```csharp
// 現在のエンドポイント定義（Program.cs）
apiRoute
    .MapPost(
        "/questions/create",
        async (
            [FromBody] CreateQuestionCommand command,
            [FromServices] SekibanOrleansExecutor executor) => await executor.CommandAsync(command).UnwrapBox())
    .WithOpenApi()
    .WithName("CreateQuestion");
```

## QuestionGroupWorkflowの既存機能レビュー

`QuestionGroupWorkflow.cs` をレビューしたところ、以下の機能がすでに実装されていることがわかりました：

1. `CreateGroupWithQuestionsAsync`: グループと質問を一度に作成するメソッド
2. `CreateQuestionAndAddToGroupAsync`: 一つの質問を作成し、指定したグループに追加する**内部**メソッド

特に、`CreateQuestionAndAddToGroupAsync` は以下のような実装となっています：

```csharp
/// <summary>
/// Creates a question and adds it to a group
/// </summary>
private async Task<ResultBox<bool>> CreateQuestionAndAddToGroupAsync(
    string text, 
    List<QuestionOption> options, 
    Guid groupId, 
    int order)
{
    // 1. Create the question
    var createQuestionResult = await _executor.CommandAsync(new CreateQuestionCommand(
        text,
        options,
        groupId
    ));
    
    // Use Conveyor to process the command result only if it was successful
    return await createQuestionResult.Conveyor(async questionResult => {
        var questionId = questionResult.PartitionKeys.AggregateId;
        
        // 2. Add the question to the group with proper order
        await _executor.CommandAsync(new AddQuestionToGroup(
            groupId, 
            questionId, 
            order
        ));
        
        return ResultBox.FromValue(true);
    });
}
```

このメソッドは`private`で、現在は`CreateGroupWithQuestionsAsync`からのみ使用されています。

## 改善計画

### 1. 既存のワークフローメソッドを拡張する

既存の内部メソッド `CreateQuestionAndAddToGroupAsync` をベースに、パブリックメソッドを追加します：

```csharp
/// <summary>
/// Creates a question and adds it to the end of a group
/// </summary>
public async Task<ResultBox<Guid>> CreateSingleQuestionAndAddToGroupAsync(
    CreateQuestionCommand command)
{
    // 1. グループ内の質問数を取得して、新しい質問を最後に追加
    var questionsInGroup = await _executor.QueryAsync(
        new GetQuestionsByGroupIdQuery(command.QuestionGroupId));
    
    int order = questionsInGroup.IsSuccess ? 
        questionsInGroup.GetValue().Items.Count : 0;
    
    // 2. 既存の内部メソッドを使用して質問を作成しグループに追加
    var result = await CreateQuestionAndAddToGroupAsync(
        command.Text,
        command.Options,
        command.QuestionGroupId,
        order);
    
    // 3. 成功した場合、質問IDを返す
    return result.Conveyor(success => {
        // クエリを実行して質問IDを取得（内部メソッドだと質問IDを直接取得できないため）
        return _executor.QueryAsync(
            new QuestionDetailQuery(/* 最新の質問ID */)).Conveyor(
                question => ResultBox.FromValue(question.Id));
    });
}
```

### 2. より効率的なアプローチ: CreateQuestionCommand を直接使用する

既存の内部メソッドを修正して CreateQuestionCommand を直接受け取るようにする方法もあります：

```csharp
/// <summary>
/// Creates a question and adds it to a group with a specified order
/// </summary>
public async Task<ResultBox<Guid>> CreateQuestionAndAddToGroupAsync(
    CreateQuestionCommand command,
    int order)
{
    // 1. Create the question
    var createQuestionResult = await _executor.CommandAsync(command);
    
    // Use Conveyor to process the command result only if it was successful
    return await createQuestionResult.Conveyor(async questionResult => {
        var questionId = questionResult.PartitionKeys.AggregateId;
        
        // 2. Add the question to the group with proper order
        await _executor.CommandAsync(new AddQuestionToGroup(
            command.QuestionGroupId, 
            questionId, 
            order
        ));
        
        return ResultBox.FromValue(questionId);
    });
}

/// <summary>
/// Creates a question and adds it to the end of a group
/// </summary>
public async Task<ResultBox<Guid>> CreateQuestionAndAddToGroupEndAsync(
    CreateQuestionCommand command)
{
    // グループ内の質問数を取得して、新しい質問を最後に追加
    var questionsInGroup = await _executor.QueryAsync(
        new GetQuestionsByGroupIdQuery(command.QuestionGroupId));
    
    int order = questionsInGroup.IsSuccess ? 
        questionsInGroup.GetValue().Items.Count : 0;
    
    // 上記のメソッドを使用
    return await CreateQuestionAndAddToGroupAsync(command, order);
}
```

### 3. Program.csの`/api/questions/create`エンドポイントの修正

現在のエンドポイントをワークフローを使用するシンプルな実装に修正します：

```csharp
apiRoute
    .MapPost(
        "/questions/create",
        async (
            [FromBody] CreateQuestionCommand command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // Workflowを作成して呼び出すだけのシンプルな実装
            var workflow = new QuestionGroupWorkflow(executor);
            return await workflow.CreateQuestionAndAddToGroupEndAsync(command).UnwrapBox();
        })
    .WithOpenApi()
    .WithName("CreateQuestion");
```

## 実装手順

1. QuestionGroupWorkflowクラスに、新しいパブリックメソッドを追加
   - 既存の内部メソッドを活用する方法、または
   - CreateQuestionCommand を直接受け取る新しいメソッド（推奨）
2. Program.csの`/api/questions/create`エンドポイントを修正して、Workflowを呼び出すだけのシンプルな実装にする
3. 必要に応じてクライアント側の実装を修正（現状のパラメータでは変更不要と思われる）

## 利点

1. 質問作成と同時にグループへの追加を自動的に行える
2. 新しい質問のOrderが適切に（グループ内の最後に）設定される
3. 一貫したワークフロー処理を実現できる
4. **既存の実装を最大限に活用できる**

## 注意点

1. 既存のAPIの互換性を維持する（パラメータは変更しない）
2. エラーハンドリングを適切に行う
3. トランザクション性を確保する（質問作成に成功しつつグループ追加に失敗した場合の対応）
4. 既存の内部メソッドの修正が必要な場合は、すでに使用されている箇所（`CreateGroupWithQuestionsAsync`）への影響を考慮する
