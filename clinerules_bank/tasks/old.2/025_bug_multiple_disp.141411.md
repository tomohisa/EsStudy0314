# GitHub Copilot

# 複数グループの質問表示問題分析と解決計画

## 問題の概要

現在、異なるグループ間で質問表示が互いに干渉する問題が発生しています：
- **グループ1**の質問1がStartDisplayで表示状態の時に、**グループ2**の質問2-1をStartDisplayすると、グループ2のUniqueCodeが入力されているクライアントでも質問が表示されない
- ただし、グループ1の質問1の表示が停止されている状態であれば、グループ2の質問2-1は正常に表示される

これは、グループ間の質問表示状態が互いに干渉している状態です。各グループは独立して動作するべきです。

## 問題の原因

現在のコードを分析すると、以下の問題があります：

1. **`ActiveQuestionQuery`のフィルタリング問題**:
   - 現在のコードでは、`IsDisplayed=true`の質問をすべて取得し、グループごとのフィルタリングが不十分です
   - APIエンドポイント側でUniqueCodeによるフィルタリングが試みられていますが、十分に機能していません

2. **排他制御の問題**:
   - `QuestionDisplayWorkflow`では、質問表示時に排他制御が行われていますが、この制御が他のグループに影響を与えるべきではありません

3. **グループIDを考慮したクエリの問題**:
   - 現在はグループIDとUniqueCodeのマッピングが適切に処理されていません

## 解決策

### 1. `ActiveQuestionQuery`の修正

既に`UniqueCode`引数を持っていますが、ハンドラーの内部処理を改善します：

```csharp
public static ResultBox<ActiveQuestionRecord> HandleQuery(
    MultiProjectionState<AggregateListProjector<QuestionProjector>> projection,
    ActiveQuestionQuery query,
    IQueryContext context)
{
    // 質問のリストを取得
    var questionsWithGroup = projection.Payload.Aggregates
        .Where(m => m.Value.GetPayload() is Question)
        .Select(m => ((Question)m.Value.GetPayload(), m.Value.PartitionKeys))
        .Where(tuple => tuple.Item1.IsDisplayed)
        .ToList();
        
    // UniqueCodeが指定されている場合、該当するグループIDの質問のみをフィルタリング
    if (!string.IsNullOrEmpty(query.UniqueCode))
    {
        // QuestionGroupServiceを使用して、UniqueCodeからグループIDを取得
        // 注：静的メソッドでは注入できないため、実際の実装はAPIエンドポイントで行う
        // このコメントは実装時に削除する
    }
    
    // 最終的に表示する質問を選択
    var activeQuestion = questionsWithGroup
        .Select(tuple => new ActiveQuestionRecord(
            tuple.PartitionKeys.AggregateId,
            tuple.Item1.Text,
            tuple.Item1.Options,
            tuple.Item1.Responses.Select(r => new ResponseRecord(
                r.Id,
                r.ParticipantName,
                r.SelectedOptionId,
                r.Comment,
                r.Timestamp)).ToList(),
            tuple.Item1.QuestionGroupId))
        .FirstOrDefault();

    return activeQuestion != null 
        ? activeQuestion.ToResultBox() 
        : new ActiveQuestionRecord(
            Guid.Empty, 
            string.Empty, 
            new List<QuestionOption>(), 
            new List<ResponseRecord>(),
            Guid.Empty).ToResultBox();
}
```

### 2. APIエンドポイントの修正

`Program.cs`の`/questions/active`エンドポイントを以下のように修正します：

```csharp
apiRoute.MapGet("/questions/active", async (
    [FromServices] SekibanOrleansExecutor executor,
    [FromQuery] string? uniqueCode = null) =>
{
    // UniqueCodeが指定されていない場合は空の結果を返す
    if (string.IsNullOrWhiteSpace(uniqueCode))
    {
        return new ActiveQuestionQuery.ActiveQuestionRecord(
            Guid.Empty,
            string.Empty,
            new List<QuestionOption>(),
            new List<ActiveQuestionQuery.ResponseRecord>(),
            Guid.Empty);
    }
    
    // QuestionGroupServiceをその場で生成
    var groupService = new EsCQRSQuestions.Domain.Services.QuestionGroupService(executor);
    
    // UniqueCodeからグループIDを取得
    Guid? groupId = null;
    if (!string.IsNullOrWhiteSpace(uniqueCode))
    {
        groupId = await groupService.GetGroupIdByUniqueCodeAsync(uniqueCode);
    }
    
    // アクティブな質問を取得
    var activeQuestion = await executor.QueryAsync(new ActiveQuestionQuery(uniqueCode)).UnwrapBox();
    
    // UniqueCodeが指定され、かつグループIDが見つかった場合のみフィルタリング
    if (!string.IsNullOrWhiteSpace(uniqueCode) && groupId.HasValue && activeQuestion.QuestionId != Guid.Empty)
    {
        // 質問が指定されたグループに属していない場合は空の結果を返す
        if (activeQuestion.QuestionGroupId != groupId.Value)
        {
            return new ActiveQuestionQuery.ActiveQuestionRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                new List<ActiveQuestionQuery.ResponseRecord>(),
                Guid.Empty);
        }
    }
    
    return activeQuestion;
});
```

### 3. `QuestionGroupService`の確認と修正

既存の`QuestionGroupService`が正しく動作していることを確認します：

```csharp
public class QuestionGroupService
{
    private readonly ISekibanExecutor _executor;
    
    public QuestionGroupService(ISekibanExecutor executor)
    {
        _executor = executor;
    }
    
    public async Task<Guid?> GetGroupIdByUniqueCodeAsync(string uniqueCode)
    {
        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            return null;
        }
        
        var groupsResult = await _executor.QueryAsync(new GetQuestionGroupsQuery());
        if (!groupsResult.IsSuccess)
        {
            return null;
        }
        
        var groups = groupsResult.GetValue();
        var group = groups.Items.FirstOrDefault(g => g.UniqueCode == uniqueCode);
        return group?.Id;
    }
}
```

### 4. `QuestionDisplayWorkflow`の修正

排他制御を行う`StartDisplayQuestionExclusivelyAsync`メソッドを修正して、同じグループ内の質問のみを停止するようにします：

```csharp
public async Task<ResultBox<object>> StartDisplayQuestionExclusivelyAsync(Guid questionId)
{
    // 1. 指定された質問の情報を取得してグループIDを確認
    var questionsResult = await _executor.QueryAsync(new QuestionsQuery(string.Empty));
    
    return await questionsResult.Conveyor(async result => {
        // 対象の質問を見つける
        var questionDetail = result.Items.FirstOrDefault(q => q.QuestionId == questionId);
        if (questionDetail == null)
        {
            return ResultBox.FromException<object>(new Exception($"質問が見つかりません: {questionId}"));
        }
        
        var groupId = questionDetail.QuestionGroupId;
        
        // 2. そのグループ内の質問で表示中のものを全て検索 - 同じグループIDに限定する
        var groupQuestions = await _executor.QueryAsync(
            new QuestionsQuery(string.Empty, groupId));
        
        // 処理継続
        return await groupQuestions.Conveyor(async questions => {
            // 3. 表示中の質問があれば停止する - 同じグループ内の質問のみ
            var displayingQuestions = questions.Items
                .Where(q => q.IsDisplayed && q.QuestionId != questionId)
                .ToList();
            
            // 一つずつ停止コマンドを実行
            foreach (var displayingQuestion in displayingQuestions)
            {
                await _executor.CommandAsync(new StopDisplayCommand(displayingQuestion.QuestionId));
            }
            
            // 4. 質問を表示する
            var startDisplayResult = await _executor.CommandAsync(new StartDisplayCommand(questionId));
            
            return startDisplayResult.Conveyor(r => 
                ResultBox.FromValue<object>(new { Success = true, Message = "質問表示を開始しました" }));
        });
    });
}
```

### 5. Webクライアント側の確認

`Questionair.razor`でUniqueCodeを正しく渡していることを確認します：

```csharp
private async Task RefreshActiveQuestion()
{
    try
    {
        Console.WriteLine($"Refreshing active question for UniqueCode: {UniqueCode ?? "none"}");
        // UniqueCodeを渡してフィルタリングされた質問を取得
        activeQuestion = await QuestionApi.GetActiveQuestionAsync(UniqueCode);
        // 他の処理...
    }
    catch (Exception ex)
    {
        // エラー処理...
    }
}
```

## 実装の利点

この修正により：

1. 各クライアントは自分が所属するグループの質問のみを表示できるようになります
2. グループ間で質問表示状態が互いに干渉しなくなります
3. 既存のドメインモデルを大きく変更せずに実現できます

## 実装計画

1. `Program.cs`の`/questions/active`エンドポイントのフィルタリングロジックを確認・修正
2. `QuestionDisplayWorkflow`の`StartDisplayQuestionExclusivelyAsync`メソッドを修正
3. APIエンドポイントとクライアント側の通信を確認
4. テスト：複数のグループで同時に質問表示を行い、互いに干渉しないことを確認

これらの修正により、グループ1の状態に関係なく、グループ2のクライアントがグループ2の中の質問表示ボタンを押したときに正しく質問が表示されるようになります。
