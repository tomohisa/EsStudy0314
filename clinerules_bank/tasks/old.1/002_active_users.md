Active Users の表示が変わっていない。
ActiveUsersCreated イベントは保存されているみたい。
EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor の右上のカウンターが上がらない。
上がらない理由を調査して教えて欲しい。
理由の調査だけで、コードは変更しないこと。

このファイルに、調査結果を書くこと


--- 調査結果はこの下 ---

# Active Users カウンターが更新されない問題の調査結果

## 問題の概要
Planning.razor の右上にある Active Users カウンターが更新されていない。ActiveUsersCreated イベントは保存されているが、カウンターに反映されていない。

## 調査方法
1. コードの詳細な分析
2. アプリケーションの実行と動作確認
3. ブラウザでの確認（Active Users カウンターが「0」と表示されていることを確認）

## 問題の原因

調査の結果、以下の問題点が特定されました：

### 1. CORS設定の問題
ApiService の Program.cs では、CORS ポリシーが特定のオリジン（https://localhost:7201）のみを許可するように設定されています：

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7201")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

しかし、AdminWeb アプリケーションは異なるポート（実際には https://localhost:5260）で実行されている可能性があります。これにより、SignalR 接続が確立できず、イベントを受信できない可能性があります。

### 2. SignalR 接続の問題
SignalR 接続が正しく確立されていないか、イベントハンドラーが正しく登録されていない可能性があります。Planning.razor コンポーネントでは、SignalR 接続を次のように設定しています：

```csharp
hubConnection = new HubConnectionBuilder()
    .WithUrlWithClientFactory("https+http://apiservice/questionHub", HttpMessageHandlerFactory)
    .Build();
```

この URL が正しく解決されていない可能性があります。

### 3. activeUsersId の設定問題
Planning.razor コンポーネントでは、SignalR 接続を通じて "ActiveUsersCreated" イベントを受信した際に `activeUsersId` を設定しています：

```csharp
hubConnection.On<object>("ActiveUsersCreated", async (data) =>
{
    try
    {
        // Extract the aggregate ID from the data
        var aggregateId = (data as dynamic)?.AggregateId;
        if (aggregateId != null)
        {
            activeUsersId = Guid.Parse(aggregateId.ToString());
            await RefreshActiveUsers();
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error handling ActiveUsersCreated event: {ex.Message}");
    }
});
```

このイベントが正しく受信されていないか、aggregate ID が正しく抽出されていない可能性があります。

### 4. TotalCount の計算問題
ActiveUsersProjector での TotalCount の計算方法に問題がある可能性があります：

```csharp
// Add a new user connection
(ActiveUsersAggregate activeUsers, UserConnected connected) => activeUsers with
{
    Users = activeUsers.Users
        .Where(u => u.ConnectionId != connected.ConnectionId)
        .Append(new ActiveUser(
            connected.ConnectionId,
            connected.Name,
            connected.ConnectedAt,
            connected.ConnectedAt))
        .ToList(),
    TotalCount = activeUsers.Users.Count(u => u.ConnectionId != connected.ConnectionId) + 1
}
```

この計算は、同じ ConnectionId を持つ既存のユーザーをフィルタリングした後のユーザー数に基づいています。これは重複を避けるためには正しいですが、TotalCount が正しく更新されない原因となる可能性があります。

## 最も可能性の高い原因

最も可能性が高いのは、**CORS設定の問題**と**SignalR 接続の問題**です。AdminWeb アプリケーションが実行されているポートが CORS ポリシーで許可されていないため、SignalR 接続が確立できず、イベントを受信できていない可能性があります。

## 解決策の提案

1. **CORS設定の修正**：
   ApiService の Program.cs の CORS ポリシーを修正して、AdminWeb アプリケーションが実行されているポートを許可するようにします。または、ワイルドカードを使用して任意のオリジンを許可することも考えられます（開発環境のみ）。

   ```csharp
   policy.WithOrigins("https://localhost:5260", "https://localhost:7201")
   // または
   policy.AllowAnyOrigin()
   ```

2. **SignalR 接続の改善**：
   SignalR 接続の URL を明示的に指定し、自動再接続を有効にします。

   ```csharp
   hubConnection = new HubConnectionBuilder()
       .WithUrl("https://localhost:5001/questionHub")
       .WithAutomaticReconnect()
       .Build();
   ```

3. **デバッグログの追加**：
   SignalR 接続とイベント処理に詳細なログを追加して、問題を特定しやすくします。

   ```csharp
   Console.WriteLine($"SignalR connection state: {hubConnection.State}");
   Console.WriteLine($"Received event: {eventType} with data: {data}");
   ```

これらの修正を適用することで、Active Users カウンターが正しく更新されるようになると考えられます。
