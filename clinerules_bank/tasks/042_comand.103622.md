# GitHub Copilot

## LastSortableUniqueIdを返すようにするための計画（更新版）

タスク: EsCQRSQuestions/EsCQRSQuestions.ApiService/Program.csのコマンドや、Workflow内部でコマンドを実行するものが、LastSortableUniqueIdを返すように修正する計画を立てる。

### 現状分析の詳細

コードベースを詳しく分析したところ、以下のことが分かりました：

1. 既存のエンドポイントでは `ToSimpleCommandResponse()` メソッドがどこにも使われていない
2. Sekiban.Pure.Command名前空間がCommand関連の型を定義している
3. CommandResponse型と考えられるものはSekiban.Pure.Command内にあるはず
4. ワークフロー内部でCommandの実行結果（ResultBox<CommandResponse>型）に対して変換が必要

### 拡張メソッドの実装

まず、ToSimpleCommandResponse拡張メソッドが存在しないか、使用されていない可能性があるため、以下の拡張メソッドを実装する計画です：

```csharp
// CommandExtensions.cs
using ResultBoxes;
using Sekiban.Pure.Command;

namespace EsCQRSQuestions.Domain.Extensions
{
    public static class CommandExtensions
    {
        /// <summary>
        /// CommandResponseをLastSortableUniqueIdを含む簡易形式に変換します
        /// </summary>
        public static ResultBox<CommandResponseSimple> ToSimpleCommandResponse(this ResultBox<CommandResponse> response)
        {
            return response.Conveyor(commandResponse => 
                ResultBox.FromValue(new CommandResponseSimple(
                    commandResponse.PartitionKeys.AggregateId,
                    commandResponse.LastSortableUniqueId
                ))
            );
        }
    }

    /// <summary>
    /// CommandResponseのシンプルな表現
    /// </summary>
    public record CommandResponseSimple(
        Guid AggregateId,
        string LastSortableUniqueId
    );
}
```

この拡張メソッドを使用することで、ワークフローからLastSortableUniqueIdを含む結果を返せるようになります。

### 修正対象と実装方法（詳細版）

#### 1. QuestionGroupWorkflow

**CreateGroupWithQuestionsAsync**
```csharp
public async Task<ResultBox<CommandResponseSimple>> CreateGroupWithQuestionsAsync(CreateGroupWithQuestionsCommand command)
{
    // 1. Create the question group first
    var groupCommandResult = await executor.CommandAsync(new CreateQuestionGroup(command.GroupName));
    
    // ToSimpleCommandResponseを使用
    return await groupCommandResult.ToSimpleCommandResponse().Conveyor(async simpleResponse => {
        var groupId = simpleResponse.AggregateId;
        
        // 質問を追加
        var questionTasks = new List<Task<ResultBox<CommandResponseSimple>>>();
        int order = 0;
        
        foreach (var (text, options) in command.Questions)
        {
            var task = CreateQuestionAndAddToGroupAsync(text, options, groupId, order++);
            questionTasks.Add(task);
        }
        
        // 全ての質問を追加完了
        await Task.WhenAll(questionTasks);
        
        // 最初のグループ作成結果を返す
        return ResultBox.FromValue(simpleResponse);
    });
}
```

**CreateQuestionAndAddToGroupAsync (private)**
```csharp
private async Task<ResultBox<CommandResponseSimple>> CreateQuestionAndAddToGroupAsync(
    string text, 
    List<QuestionOption> options, 
    Guid groupId, 
    int order)
{
    // 1. 質問を作成
    var createQuestionResult = await executor.CommandAsync(new CreateQuestionCommand(
        text,
        options,
        groupId
    ));
    
    // 2. グループに追加
    return await createQuestionResult.ToSimpleCommandResponse().Conveyor(async questionResponse => {
        var questionId = questionResponse.AggregateId;
        
        var addToGroupResult = await executor.CommandAsync(new AddQuestionToGroup(
            groupId, 
            questionId, 
            order
        ));
        
        // 最後のコマンド実行結果を返す
        return addToGroupResult.ToSimpleCommandResponse();
    });
}
```

**CreateQuestionAndAddToGroupEndAsync**
```csharp
public async Task<ResultBox<CommandResponseSimple>> CreateQuestionAndAddToGroupEndAsync(
    CreateQuestionCommand command)
{
    // グループ内の質問数を取得
    var questionsInGroup = await executor.QueryAsync(
        new GetQuestionsByGroupIdQuery(command.QuestionGroupId));
    
    int order = questionsInGroup.IsSuccess ? 
        questionsInGroup.GetValue().Items.Count() : 0;
    
    // 上記のメソッドを使用 (戻り値の型が変更されている)
    return await CreateQuestionAndAddToGroupAsync(command, order);
}
```

**CreateGroupWithUniqueCodeAsync**
```csharp
public async Task<ResultBox<CommandResponseSimple>> CreateGroupWithUniqueCodeAsync(
    string groupName, 
    string uniqueCode = "")
{
    // UniqueCodeの生成ロジックは変更なし
    if (string.IsNullOrEmpty(uniqueCode))
    {
        var codeResult = await GenerateUniqueCodeAsync();
        if (!codeResult.IsSuccess)
        {
            return ResultBox.FromException<CommandResponseSimple>(codeResult.GetException());
        }
        uniqueCode = codeResult.GetValue();
    }
    else
    {
        var isValid = await ValidateUniqueCodeAsync(uniqueCode);
        if (!isValid)
        {
            return ResultBox.FromException<CommandResponseSimple>(
                new InvalidOperationException($"UniqueCode '{uniqueCode}' is already in use"));
        }
    }
    
    // グループ作成コマンドを実行し、SimpleCommandResponseを返す
    var groupCommandResult = await executor.CommandAsync(
        new CreateQuestionGroup(groupName, uniqueCode));
    
    // ToSimpleCommandResponseを使用
    return groupCommandResult.ToSimpleCommandResponse();
}
```

#### 2. QuestionDisplayWorkflow

**StartDisplayQuestionExclusivelyAsync**
```csharp
public async Task<ResultBox<CommandResponseSimple>> StartDisplayQuestionExclusivelyAsync(
    Guid questionId)
{
    // 質問情報の取得部分は変更なし
    var questionsResult = await executor.QueryAsync(new QuestionsQuery(string.Empty));
    
    return await questionsResult.Conveyor(async result => {
        // 対象の質問を見つける
        var questionDetail = result.Items.FirstOrDefault(q => q.QuestionId == questionId);
        if (questionDetail == null)
        {
            return ResultBox.FromException<CommandResponseSimple>(
                new Exception($"質問が見つかりません: {questionId}"));
        }
        
        var groupId = questionDetail.QuestionGroupId;
        
        // グループ内の質問を検索
        var groupQuestions = await executor.QueryAsync(
            new QuestionsQuery(string.Empty, groupId));
        
        // 処理継続
        return await groupQuestions.Conveyor(async questions => {
            // 表示中の質問があれば停止する
            var displayingQuestions = questions.Items
                .Where(q => q.IsDisplayed && q.QuestionId != questionId)
                .ToList();
            
            // 一つずつ停止コマンドを実行
            foreach (var displayingQuestion in displayingQuestions)
            {
                await executor.CommandAsync(new StopDisplayCommand(displayingQuestion.QuestionId));
            }
            
            // 指定された質問を表示状態にし、SimpleCommandResponseを返す
            var startResult = await executor.CommandAsync(new StartDisplayCommand(questionId));
            return startResult.ToSimpleCommandResponse();
        });
    });
}
```

### Program.csの修正

各エンドポイントでワークフローを使用している箇所も修正します：

```csharp
// /questions/create エンドポイント
apiRoute
    .MapPost(
        "/questions/create",
        async (
            [FromBody] CreateQuestionCommand command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // ワークフローが直接CommandResponseSimpleを返すようになった
            var workflow = new QuestionGroupWorkflow(executor);
            return await workflow.CreateQuestionAndAddToGroupEndAsync(command).UnwrapBox();
        })
    .WithOpenApi()
    .WithName("CreateQuestion");

// /questions/startDisplay エンドポイント
apiRoute
    .MapPost(
        "/questions/startDisplay",
        async (
            [FromBody] StartDisplayCommand command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // ワークフローが直接CommandResponseSimpleを返すようになった
            var workflow = new QuestionDisplayWorkflow(executor);
            return await workflow.StartDisplayQuestionExclusivelyAsync(command.QuestionId).UnwrapBox();
        })
    .WithOpenApi()
    .WithName("StartDisplayQuestion");

// /questionGroups/createWithUniqueCode エンドポイント
apiRoute
    .MapPost(
        "/questionGroups/createWithUniqueCode",
        async (
            [FromBody] CreateQuestionGroup command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // ワークフローが直接CommandResponseSimpleを返すようになった
            var workflow = new QuestionGroupWorkflow(executor);
            var result = await workflow.CreateGroupWithUniqueCodeAsync(
                command.Name, command.UniqueCode);
                
            return result.Match(
                response => Results.Ok(new { 
                    GroupId = response.AggregateId,
                    LastSortableUniqueId = response.LastSortableUniqueId
                }),
                error => Results.Problem(error.Message)
            );
        })
    .WithOpenApi()
    .WithName("CreateQuestionGroupWithUniqueCode");
```

### 実装ステップ

1. CommandExtensions.csファイルの作成とCommandResponseSimpleレコードの実装
2. QuestionGroupWorkflowの各メソッドを修正
3. QuestionDisplayWorkflowの修正
4. Program.csのエンドポイント実装の修正
5. テスト実行による検証

この計画に従って実装することで、各ワークフローがLastSortableUniqueIdを含む一貫したレスポンスを返せるようになります。また、ToSimpleCommandResponse拡張メソッドを導入することで、他の場所でも同様のパターンを使いやすくなります。😊
