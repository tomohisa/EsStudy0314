0195a6f7-dfff-75a7-b99f-36a0552a8eca

clinerules_bank/tasks/002_active_users.md
で問題を洗い出しましたが、問題は、ActiveUserId 集約IDがわからないことです。現時点で、サイト単位で１サーベイなので、固定集約IDとしていいです。1行目の値を固定で、Domainで使用してください。登録側からは、CommandにIDを設定してください。

--- 実装結果はこの下 ---

# Active Users カウンター問題の修正

## 問題の概要
Active Users カウンターが更新されない問題がありました。調査の結果、ActiveUsersId が正しく設定・取得されていないことが原因でした。

## 修正内容

### 1. CreateActiveUsersCommand の修正
固定の集約ID（0195a6f7-dfff-75a7-b99f-36a0552a8eca）を使用するように変更しました。

```csharp
[GenerateSerializer]
public record CreateActiveUsersCommand() : ICommandWithHandler<CreateActiveUsersCommand, ActiveUsersProjector>
{
    // 固定集約ID
    private static readonly Guid FixedActiveUsersId = Guid.Parse("0195a6f7-dfff-75a7-b99f-36a0552a8eca");
    
    public PartitionKeys SpecifyPartitionKeys(CreateActiveUsersCommand command) => 
        PartitionKeys.Existing<ActiveUsersProjector>(FixedActiveUsersId);

    public ResultBox<EventOrNone> Handle(CreateActiveUsersCommand command, ICommandContext<IAggregatePayload> context)
    {
        // Create the event
        return EventOrNone.Event(new ActiveUsersCreated());
    }
}
```

### 2. QuestionHub.cs の修正
QuestionHub クラスでも固定の集約ID を使用するように変更しました。

```csharp
public class QuestionHub : Hub
{
    private readonly SekibanOrleansExecutor _executor;
    
    // 固定集約ID
    private static readonly Guid _activeUsersId = Guid.Parse("0195a6f7-dfff-75a7-b99f-36a0552a8eca");
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    // ...
}
```

また、TrackUserConnection メソッドも修正して、固定の集約ID を使用するようにしました。

```csharp
private async Task TrackUserConnection()
{
    try
    {
        // Ensure we have an ActiveUsers aggregate
        await _semaphore.WaitAsync();
        try
        {
            // Create the ActiveUsers aggregate with the fixed ID if it doesn't exist
            await _executor.CommandAsync(new CreateActiveUsersCommand());
        }
        finally
        {
            _semaphore.Release();
        }
        
        // Track the user connection
        string? name = null;
        if (Context.Items.TryGetValue("ParticipantName", out var nameObj) && nameObj is string nameStr)
        {
            name = nameStr;
        }
        
        await _executor.CommandAsync(new UserConnectedCommand(
            _activeUsersId,
            Context.ConnectionId,
            name));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error tracking user connection: {ex.Message}");
    }
}
```

### 3. Planning.razor の修正
Planning.razor ファイルでも固定の集約ID を使用するように変更しました。

```csharp
@code {
    // ...
    // 固定集約ID
    private readonly Guid activeUsersId = Guid.Parse("0195a6f7-dfff-75a7-b99f-36a0552a8eca");
    // ...
}
```

また、RefreshActiveUsers メソッドも修正して、固定の集約ID を使用するようにしました。

```csharp
private async Task RefreshActiveUsers()
{
    try
    {
        Console.WriteLine($"Refreshing active users with ID {activeUsersId}...");
        activeUsers = await ActiveUsersApi.GetActiveUsersAsync(activeUsersId);
        Console.WriteLine($"Active users count: {activeUsers?.TotalCount ?? 0}");
        await InvokeAsync(() => StateHasChanged());
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error refreshing active users: {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
    }
}
```

### 4. CORS 設定の修正
ApiService の Program.cs ファイルの CORS 設定を修正して、AdminWeb アプリケーションからの接続を許可するようにしました。

```csharp
// Add CORS services and configure a policy that allows specific origins with credentials
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7201", "https://localhost:5260")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

## 結果
これらの修正により、Active Users カウンターが正しく更新されるようになりました。カウンターは現在「Active Users: 1」と表示されています。

## 学んだこと
1. Event Sourcing システムでは、集約ID の管理が重要です。
2. 固定の集約ID を使用することで、複数のコンポーネント間で同じ集約を参照できます。
3. CORS 設定は、SignalR 接続などのリアルタイム通信に重要な役割を果たします。
