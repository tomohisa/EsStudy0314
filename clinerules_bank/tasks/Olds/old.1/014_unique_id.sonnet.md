# QuestionGroupにUniqueCodeを追加する設計

## 概要

QuestionGroupに6桁の英数字からなるUniqueCodeを追加します。これにより管理や識別が容易になります。コードは自動生成され、既存のアクティブなQuestionGroupと重複しないようにチェックされます。

## 機能要件

1. QuestionGroupに6桁の英数字（UniqueCode）を追加する
2. 新規グループ作成時にランダムなUniqueCodeを自動生成する
3. 生成されたコードが既存のアクティブなQuestionGroupのコードと重複しないことを確認する
4. 管理画面でUniqueCodeを表示する

## 技術設計

### 1. ドメインモデルの変更

#### QuestionGroup.cs（ペイロード）の更新

```csharp
[GenerateSerializer, Immutable]
public record QuestionGroup(
    string Name,
    string UniqueCode,  // 新規追加：6桁の英数字
    List<QuestionReference> Questions) : IAggregatePayload
{
    public QuestionGroup() : this("", "", new List<QuestionReference>()) { }
    
    // 既存メソッド...
    
    /// <summary>
    /// Updates the unique code of the group.
    /// </summary>
    public QuestionGroup UpdateUniqueCode(string newUniqueCode)
    {
        return this with { UniqueCode = newUniqueCode };
    }
}
```

#### QuestionGroupCreated.cs（イベント）の更新

```csharp
[GenerateSerializer]
public record QuestionGroupCreated(
    Guid GroupId, // Aggregate ID
    string Name,
    string UniqueCode, // 新規追加：6桁の英数字
    List<Guid> InitialQuestionIds // Optional: If created with initial questions
    ) : IEventPayload;
```

#### CreateQuestionGroup.cs（コマンド）の更新

```csharp
[GenerateSerializer]
public record CreateQuestionGroup(
    string Name, 
    string UniqueCode = "") : ICommandWithHandler<CreateQuestionGroup, QuestionGroupProjector>
{
    public PartitionKeys SpecifyPartitionKeys(CreateQuestionGroup command) => 
        PartitionKeys.Generate<QuestionGroupProjector>();

    public ResultBox<EventOrNone> Handle(CreateQuestionGroup command, ICommandContext<IAggregatePayload> context)
        => context.GetAggregate()
            .Conveyor(aggregate => {
                var groupId = aggregate.PartitionKeys.AggregateId;
                string uniqueCode = string.IsNullOrEmpty(command.UniqueCode) ? 
                    GenerateRandomCode() : command.UniqueCode;
                    
                return EventOrNone.Event(new QuestionGroupCreated(
                    groupId, command.Name, uniqueCode, new List<Guid>()));
            });
            
    private static string GenerateRandomCode()
    {
        // 英数字からランダムに6文字を選択
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
```

#### QuestionGroupProjector.cs の更新

```csharp
public IAggregatePayload Project(IAggregatePayload payload, IEvent ev)
    => (payload, ev.GetPayload()) switch
    {
        // Initial creation
        (EmptyAggregatePayload, QuestionGroupCreated e) =>
            new QuestionGroup(
                e.Name,
                e.UniqueCode, // 新規追加：UniqueCodeの適用
                e.InitialQuestionIds?.Select((id, index) => new QuestionReference(id, index)).ToList() ?? new()
            ),
            
        // その他の既存ケース...
    };
```

### 2. ワークフローの追加

#### QuestionGroupWorkflow.cs に重複チェック機能を追加

```csharp
/// <summary>
/// 重複しないUniqueCodeを生成して検証するワークフロー
/// </summary>
public async Task<ResultBox<string>> GenerateUniqueCodeAsync()
{
    // 6桁のランダムコードを生成
    var uniqueCode = GenerateRandomCode();
    
    // 重複チェック
    var isValid = await ValidateUniqueCodeAsync(uniqueCode);
    
    if (isValid)
    {
        return ResultBox.FromValue(uniqueCode);
    }
    
    // 最大10回まで再試行
    for (int i = 0; i < 10; i++)
    {
        uniqueCode = GenerateRandomCode();
        isValid = await ValidateUniqueCodeAsync(uniqueCode);
        
        if (isValid)
        {
            return ResultBox.FromValue(uniqueCode);
        }
    }
    
    // 10回試行しても重複が解消しない場合はエラー
    return ResultBox.FromException<string>(
        new InvalidOperationException("Failed to generate a unique code after multiple attempts"));
}

/// <summary>
/// 生成されたUniqueCodeが既存のアクティブなQuestionGroupと重複しないことを確認
/// </summary>
private async Task<bool> ValidateUniqueCodeAsync(string uniqueCode)
{
    // 全QuestionGroupを取得
    var groupsResult = await _executor.QueryAsync(new GetQuestionGroupsQuery());
    
    if (!groupsResult.IsSuccess)
    {
        return false;
    }
    
    var groups = groupsResult.GetValue();
    
    // 同じUniqueCodeを持つグループがないか確認
    return !groups.Items.Any(g => g.UniqueCode == uniqueCode);
}

private static string GenerateRandomCode()
{
    // 英数字からランダムに6文字を選択
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    var random = new Random();
    return new string(Enumerable.Repeat(chars, 6)
        .Select(s => s[random.Next(s.Length)]).ToArray());
}

/// <summary>
/// UniqueCodeの重複をチェックして新しいQuestionGroupを作成する
/// </summary>
public async Task<ResultBox<Guid>> CreateGroupWithUniqueCodeAsync(
    string groupName, 
    string uniqueCode = "")
{
    // UniqueCodeが指定されていない場合は生成
    if (string.IsNullOrEmpty(uniqueCode))
    {
        var codeResult = await GenerateUniqueCodeAsync();
        if (!codeResult.IsSuccess)
        {
            return codeResult.Exception;
        }
        uniqueCode = codeResult.GetValue();
    }
    else
    {
        // 指定されたUniqueCodeの重複チェック
        var isValid = await ValidateUniqueCodeAsync(uniqueCode);
        if (!isValid)
        {
            return ResultBox.FromException<Guid>(
                new InvalidOperationException($"UniqueCode '{uniqueCode}' is already in use"));
        }
    }
    
    // グループを作成
    var groupCommandResult = await _executor.CommandAsync(
        new CreateQuestionGroup(groupName, uniqueCode));
    
    // 結果を返す
    return groupCommandResult.Conveyor(result => 
        ResultBox.FromValue(result.PartitionKeys.AggregateId));
}
```

### 3. API エンドポイントの追加

#### Program.cs への新規エンドポイント追加

```csharp
// 重複チェック機能を持つエンドポイント
apiRoute
    .MapPost(
        "/questionGroups/createWithUniqueCode",
        async (
            [FromBody] CreateQuestionGroup command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // ワークフローを使って重複チェックを実行
            var workflow = new QuestionGroupWorkflow(executor);
            var result = await workflow.CreateGroupWithUniqueCodeAsync(
                command.Name, command.UniqueCode);
                
            return result.Match(
                groupId => Results.Ok(new { GroupId = groupId }),
                error => Results.Problem(error.Message)
            );
        })
    .WithOpenApi()
    .WithName("CreateQuestionGroupWithUniqueCode");
```

### 4. UI 更新（Planning.razor）

```razor
<div class="mb-3">
    <label>Unique Code: @questionGroup.UniqueCode</label>
</div>
```

## 実装手順

1. QuestionGroup.cs ペイロードを更新し、UniqueCodeプロパティを追加
2. QuestionGroupCreated.cs イベントを更新し、UniqueCodeを追加
3. CreateQuestionGroup.cs コマンドを更新し、UniqueCodeパラメータとランダム生成ロジックを追加
4. QuestionGroupProjector.cs プロジェクターを更新して、イベントからUniqueCodeを適用
5. QuestionGroupWorkflow.cs ワークフローに重複チェック機能を実装
6. Program.cs に新しいAPIエンドポイントを追加
7. Planning.razor に表示機能を追加

## 注意事項

1. UniqueCodeは大文字アルファベットと数字の組み合わせでわかりやすさを重視
2. 重複チェックは既存の全グループに対して行う
3. 自動生成コードが重複する確率は低いが、複数回の再試行で対応
4. 将来的にはUniqueCodeを使った直接クエリも検討
