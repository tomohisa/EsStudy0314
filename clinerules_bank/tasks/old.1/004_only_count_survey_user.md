EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor 
のカウンターの問題2つ

1. リアルタイム動作が動いていない。リスタートしたら数値が変わるが、別のブラウザで画面を開いても多分signalrの通知が届いていない
2. カウントしたいのは、surveyを受ける人だけなので、管理画面のカウントはしないようにしてほしい

問題の修正方法を考えて、日本語で以下に対策を書いてください。
------

# Active Users カウンターの問題修正方法（更新版）

## 問題の分析

### 問題1: リアルタイム動作が機能していない
現在の実装では、SignalRを使用してリアルタイム通知を行っていますが、管理画面（Planning.razor）でActiveUsersの更新が正しく反映されていません。リスタートすると数値が変わりますが、別のブラウザで画面を開いても更新されないため、SignalRの通知が正しく機能していない可能性があります。

### 問題2: 管理画面のユーザーもカウントされている
現在の実装では、すべてのクライアント接続（管理画面を含む）がParticipantsグループに追加され、ActiveUsersとしてカウントされています。しかし、実際にカウントしたいのはsurveyを受ける参加者のみです。管理者はカウントから除外する必要があります。

## 修正方法

### 1. リアルタイム通知の修正

現在、ActiveUsers関連のイベントは管理者グループ（Admins）にのみ通知されていますが、これは正しい設計です。クライアント（参加者）側では表示の必要がないため、管理者側への通知だけで十分です。問題は通知の受信処理にあると考えられます。

#### Planning.razorの修正
SignalR接続の初期化と通知の受信処理を改善します。

```csharp
// Set up SignalR connection
hubConnection = new HubConnectionBuilder()
    .WithUrlWithClientFactory("https+http://apiservice/questionHub", HttpMessageHandlerFactory)
    .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5) })  // より積極的な再接続
    .Build();

// 接続状態の変更をより詳細にログ出力
hubConnection.Closed += async (error) =>
{
    Console.Error.WriteLine($"SignalR connection closed: {error?.Message}");
    Console.Error.WriteLine($"Connection state: {hubConnection.State}");
    await Task.Delay(new Random().Next(0, 5) * 1000);
    try
    {
        await hubConnection.StartAsync();
        Console.WriteLine("SignalR connection restarted successfully");
        // 再接続後に管理者グループに再参加
        await hubConnection.InvokeAsync("JoinAdminGroup");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error restarting SignalR connection: {ex.Message}");
    }
};

// 接続状態の変更をログ出力
hubConnection.Reconnecting += (error) =>
{
    Console.WriteLine($"SignalR reconnecting: {error?.Message}");
    return Task.CompletedTask;
};

hubConnection.Reconnected += (connectionId) =>
{
    Console.WriteLine($"SignalR reconnected with connection ID: {connectionId}");
    return Task.CompletedTask;
};
```

### 2. 管理画面ユーザーのカウント除外

クライアント（参加者）のみからユーザー作成を行い、管理者はカウントしないようにします。

#### QuestionHub.csの修正
OnConnectedAsyncメソッドを修正して、デフォルトではユーザーを追加しないようにします。

```csharp
// Client connection
public override async Task OnConnectedAsync()
{
    // By default, add all clients to the Participants group
    await Groups.AddToGroupAsync(Context.ConnectionId, ParticipantGroup);
    
    // 管理者接続時はActiveUsersに追加しない
    // TrackUserConnectionは参加者専用のメソッドとして残す
    
    await base.OnConnectedAsync();
}

// Join admin group
public async Task JoinAdminGroup()
{
    await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
    
    // 管理者グループに参加したユーザーはActiveUsersから削除
    if (_activeUsersId.HasValue)
    {
        await _executor.CommandAsync(new UserDisconnectedCommand(
            _activeUsersId,
            Context.ConnectionId));
    }
}

// 参加者専用のメソッドを追加
public async Task JoinAsSurveyParticipant()
{
    // 参加者としてActiveUsersに追加
    await TrackUserConnection();
}
```

#### Questionair.razorの修正
参加者側のコードを修正して、明示的に参加者として参加するようにします。

```csharp
protected override async Task OnInitializedAsync()
{
    // 既存のコード...
    
    // Start the connection
    await hubConnection.StartAsync();
    
    // 明示的に参加者として参加
    await hubConnection.InvokeAsync("JoinAsSurveyParticipant");
    
    // 既存のコード...
}
```

#### Planning.razorの修正
管理画面側のコードを修正して、明示的に管理者として参加するようにします。

```csharp
protected override async Task OnInitializedAsync()
{
    // 既存のコード...
    
    // Start the connection
    try
    {
        Console.WriteLine("Starting SignalR connection...");
        await hubConnection.StartAsync();
        Console.WriteLine("SignalR connection started successfully");
        
        // 明示的に管理者グループに参加
        await hubConnection.InvokeAsync("JoinAdminGroup");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error starting SignalR connection: {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
    }
    
    // 既存のコード...
}
```

## 期待される結果

これらの修正により、以下の効果が期待されます：

1. **リアルタイム通知の改善**：
   - SignalR接続の安定性と再接続機能が強化され、管理画面でのリアルタイム更新が正しく機能するようになります。
   - 接続状態の詳細なログ出力により、問題が発生した場合のデバッグが容易になります。

2. **管理画面ユーザーのカウント除外**：
   - 管理画面（Planning.razor）のユーザーはActiveUsersに追加されないか、追加後に削除されるため、カウントから除外されます。
   - 参加者（Questionair.razor）のみがActiveUsersとしてカウントされます。
   - これにより、カウンターにはsurveyを受ける参加者のみが表示されるようになります。

この修正方法は、既存のコードベースに最小限の変更を加えることで実装でき、新しいイベントやコマンドを追加する必要がないため、より簡潔で実装が容易です。また、管理者と参加者の役割分担が明確になり、将来的な拡張性も向上します。
