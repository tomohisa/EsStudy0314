# UniqueCodeを使用した質問表示機能の問題点分析と解決計画

## 概要

現在、UniqueCodeのみで質問を表示する機能を実装中ですが、管理者画面から質問に対して「Start Display」ボタンを押した際に、クライアント側（参加者側）に質問が表示されない問題が発生しています。この問題を解決するために、関連するコードを分析し、改善点を特定します。

## 問題点の分析

調査した結果、以下の点が問題である可能性があります：

### 1. SignalR通知の問題

#### 管理者側 (Planning.razor)
```csharp
private async Task StartDisplayQuestion(Guid questionId)
{
    try
    {
        var uniqueCode = groups?.FirstOrDefault(g => g.Id == selectedGroupId)?.UniqueCode ?? "";
        Console.WriteLine($"Starting display for question {questionId} (UniqueCode: {uniqueCode})...");
        await HubService.StartDisplayQuestionForGroup(questionId, uniqueCode);
        Console.WriteLine("Display started");
        await RefreshQuestions();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error starting display: {ex.Message}");
    }
}
```

#### QuestionHubService.cs
```csharp
public async Task StartDisplayQuestionForGroup(Guid questionId, string uniqueCode)
{
    if (IsConnected && !string.IsNullOrWhiteSpace(uniqueCode))
    {
        await _hubConnection!.InvokeAsync("StartDisplayQuestionForGroup", questionId, uniqueCode);
    }
}
```

#### QuestionHub.cs
```csharp
public async Task StartDisplayQuestionForGroup(Guid questionId, string uniqueCode)
{
    // ここではHubNotificationServiceをDIで受け取る設計が理想だが、既存構造を壊さずにServiceLocatorで取得する例
    var hubContext = (IHubContext<QuestionHub>?)Context.GetHttpContext()?.RequestServices.GetService(typeof(IHubContext<QuestionHub>));
    var notificationService = (IHubNotificationService?)Context.GetHttpContext()?.RequestServices.GetService(typeof(IHubNotificationService));
    if (notificationService != null && !string.IsNullOrWhiteSpace(uniqueCode))
    {
        await notificationService.NotifyUniqueCodeGroupAsync(uniqueCode, "QuestionDisplayStarted", new { QuestionId = questionId });
    }
}
```

### 2. 参加者側のグループ参加の問題

#### Questionair.razor
```csharp
// Join as survey participant with unique code if available
if (!string.IsNullOrEmpty(UniqueCode))
{
    await hubConnection.InvokeAsync("JoinAsSurveyParticipant", UniqueCode);
    Console.WriteLine($"Joined survey with unique code: {UniqueCode}");
}
else
{
    await hubConnection.InvokeAsync("JoinAsSurveyParticipant");
    Console.WriteLine("Joined survey without unique code");
}
```

#### QuestionHub.cs
```csharp
public async Task JoinAsSurveyParticipant(string uniqueCode)
{
    // 参加者としてActiveUsersに追加
    await TrackUserConnection();
    if (!string.IsNullOrWhiteSpace(uniqueCode))
    {
        // UniqueCodeごとのSignalRグループに追加
        await Groups.AddToGroupAsync(Context.ConnectionId, uniqueCode);
    }
}
```

## 特定された問題点

1. **ServiceLocator パターンの問題**:
   - QuestionHub の StartDisplayQuestionForGroup メソッド内で ServiceLocator パターンを使用して IHubNotificationService を取得していますが、この方法は Context.GetHttpContext() が null を返す可能性があります。

2. **デバッグログの不足**:
   - 重要なメソッド（特に StartDisplayQuestionForGroup と NotifyUniqueCodeGroupAsync）にログ出力が不足しており、問題追跡が困難です。

3. **参加者のグループ参加確認の欠如**:
   - 参加者が正しいUniqueCodeグループに参加できているか確認する仕組みがありません。

4. **イベント伝搬の問題**:
   - QuestionDisplayStarted イベントが正しく参加者に伝搬されているか確認できません。

5. **例外処理の不足**:
   - 重要なメソッドで例外が発生した場合のロギングや処理が不十分です。

## 解決計画

### 1. ServiceLocator パターンの改善

```csharp
// QuestionHub.cs
private readonly IHubNotificationService _notificationService;

public QuestionHub(SekibanOrleansExecutor executor, IHubNotificationService notificationService)
{
    _executor = executor;
    _notificationService = notificationService;
}

public async Task StartDisplayQuestionForGroup(Guid questionId, string uniqueCode)
{
    _logger.LogInformation($"StartDisplayQuestionForGroup called: QuestionId={questionId}, UniqueCode={uniqueCode}");
    
    if (!string.IsNullOrWhiteSpace(uniqueCode))
    {
        await _notificationService.NotifyUniqueCodeGroupAsync(uniqueCode, "QuestionDisplayStarted", new { QuestionId = questionId });
        _logger.LogInformation($"Notification sent to group {uniqueCode}");
    }
    else
    {
        _logger.LogWarning("UniqueCode is empty, notification not sent");
    }
}
```

### 2. デバッグログの追加

以下の場所にデバッグログを追加します：

- HubNotificationService.cs の NotifyUniqueCodeGroupAsync メソッド
- QuestionHub.cs の JoinAsSurveyParticipant メソッド
- Questionair.razor の QuestionDisplayStarted イベントハンドラ

```csharp
// HubNotificationService.cs
public async Task NotifyUniqueCodeGroupAsync(string uniqueCode, string method, object data)
{
    Console.WriteLine($"NotifyUniqueCodeGroupAsync: UniqueCode={uniqueCode}, Method={method}");
    if (!string.IsNullOrWhiteSpace(uniqueCode))
    {
        await _hubContext.Clients.Group(uniqueCode).SendAsync(method, data);
        Console.WriteLine($"Notification sent to group {uniqueCode}");
    }
    else
    {
        Console.WriteLine("UniqueCode is empty, notification not sent");
    }
}
```

### 3. 参加者のグループ参加確認

```csharp
// QuestionHub.cs
public async Task JoinAsSurveyParticipant(string uniqueCode)
{
    Console.WriteLine($"JoinAsSurveyParticipant called: UniqueCode={uniqueCode}, ConnectionId={Context.ConnectionId}");
    
    // 参加者としてActiveUsersに追加
    await TrackUserConnection();
    
    if (!string.IsNullOrWhiteSpace(uniqueCode))
    {
        // UniqueCodeごとのSignalRグループに追加
        await Groups.AddToGroupAsync(Context.ConnectionId, uniqueCode);
        Console.WriteLine($"Added connection {Context.ConnectionId} to group {uniqueCode}");
        
        // 確認メッセージを参加者に送信
        await Clients.Caller.SendAsync("JoinedGroup", new { UniqueCode = uniqueCode });
    }
    else
    {
        Console.WriteLine($"UniqueCode is empty, not adding to specific group");
    }
}
```

### 4. イベント伝搬の検証

Questionair.razor に JoinedGroup イベントハンドラを追加し、参加者がグループに正しく参加できたことを確認します：

```csharp
// Questionair.razor
hubConnection.On<object>("JoinedGroup", group => {
    Console.WriteLine($"Joined group with UniqueCode: {(group as dynamic).UniqueCode}");
    errorMessage = "";
});
```

また、QuestionDisplayStarted イベントハンドラにもログを追加：

```csharp
hubConnection.On<object>("QuestionDisplayStarted", async data => {
    Console.WriteLine($"QuestionDisplayStarted event received: {Newtonsoft.Json.JsonConvert.SerializeObject(data)}");
    await RefreshActiveQuestion();
    hasSubmitted = false;
    selectedOptionId = "";
    comment = "";
    errorMessage = "";
    await InvokeAsync(StateHasChanged);
});
```

### 5. 例外処理の改善

各メソッドに適切な例外処理を追加します：

```csharp
// Example for NotifyUniqueCodeGroupAsync
public async Task NotifyUniqueCodeGroupAsync(string uniqueCode, string method, object data)
{
    try
    {
        Console.WriteLine($"NotifyUniqueCodeGroupAsync: UniqueCode={uniqueCode}, Method={method}");
        if (!string.IsNullOrWhiteSpace(uniqueCode))
        {
            await _hubContext.Clients.Group(uniqueCode).SendAsync(method, data);
            Console.WriteLine($"Notification sent to group {uniqueCode}");
        }
        else
        {
            Console.WriteLine("UniqueCode is empty, notification not sent");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error in NotifyUniqueCodeGroupAsync: {ex.Message}");
        // 必要に応じてエラーをログに記録
    }
}
```

## 実装計画

1. まず、デバッグログを追加し、問題の詳細な原因を特定します
2. HubNotificationService の NotifyUniqueCodeGroupAsync メソッドを改善
3. QuestionHub の StartDisplayQuestionForGroup メソッドを改善（DIを使用）
4. 参加者側のグループ参加処理とイベントハンドラを強化
5. テストを実施し、問題が解決したことを確認

## 想定される成果

これらの改善により、以下の成果が期待できます：

1. UniqueCodeに基づいて特定の参加者グループにのみ質問が表示される
2. デバッグが容易になり、将来の問題も迅速に特定できる
3. システムの耐障害性が向上し、例外発生時も適切に処理される
4. 管理者と参加者間の連携がスムーズになる

## 考慮すべき代替案

1. **IHubContext インジェクション**：
   - StartDisplayQuestionForGroup メソッドで直接 IHubContext を使用し、NotifyUniqueCodeGroupAsync をバイパスする方法も考えられますが、責務の分離の観点から現在の設計を改善する方が良いでしょう。

2. **イベントベースのアプローチ**：
   - SignalR の直接呼び出しではなく、ドメインイベントを使用して質問表示状態の変更を通知する方法も考えられます。しかし、現在の設計を大幅に変更することになるため、まずは現行アーキテクチャ内での改善を試みます。
