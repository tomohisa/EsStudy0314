# GitHub Copilot

## 問題分析

現状の実装では、質問表示を開始（Start Display）する際にはUnique Codeが正しく設定されているが、表示停止（Stop Display）する際にUnique Codeが設定されていないため、他のUnique Codeを使用しているクライアントも表示停止してしまう問題がある。

### 現在の実装の問題点

1. `Planning.razor`の`StopDisplayQuestion`メソッドでは、Unique Codeを取得しておらず、QuestionHubServiceに渡していない
2. `QuestionHubService.cs`には`StopDisplayQuestionForGroup`相当のメソッドが実装されていない
3. `QuestionHub.cs`にも`StopDisplayQuestionForGroup`メソッドが実装されていない

### 対照的な正しい実装

`StartDisplayQuestion`の実装では：
1. `Planning.razor`で選択されたグループからUnique Codeを取得
2. QuestionHubServiceの`StartDisplayQuestionForGroup`メソッドを呼び出し、questionIdとuniqueCodeを渡す
3. QuestionHubの`StartDisplayQuestionForGroup`メソッドが呼び出され、特定のUniqueCodeグループにのみ通知が送られる

## 修正計画

### 1. QuestionHubService.csの修正

`QuestionHubService.cs`に以下のメソッドを追加する：

```csharp
// Unique Codeを指定して表示停止依頼を送信
public async Task StopDisplayQuestionForGroup(Guid questionId, string uniqueCode)
{
    if (IsConnected && !string.IsNullOrWhiteSpace(uniqueCode))
    {
        await _hubConnection!.InvokeAsync("StopDisplayQuestionForGroup", questionId, uniqueCode);
    }
}
```

### 2. QuestionHub.csの修正

`QuestionHub.cs`に以下のメソッドを追加する：

```csharp
// Adminからの表示停止依頼をUniqueCodeグループにだけ通知
public async Task StopDisplayQuestionForGroup(Guid questionId, string uniqueCode)
{
    _logger.LogInformation($"StopDisplayQuestionForGroup called: QuestionId={questionId}, UniqueCode={uniqueCode}");
    
    if (!string.IsNullOrWhiteSpace(uniqueCode))
    {
        try
        {
            // 1. まずコマンドを実行し、イベントを保存
            var result = await _executor.CommandAsync(new StopDisplayCommand(questionId));
            
            if (result.IsSuccess)
            {
                _logger.LogInformation($"StopDisplayCommand executed successfully for question {questionId}");
                
                // 2. 実行成功した場合のみ、通知を送信
                await _notificationService.NotifyUniqueCodeGroupAsync(
                    uniqueCode, 
                    "QuestionDisplayStopped", 
                    new { QuestionId = questionId });
                
                _logger.LogInformation($"Notification sent to group {uniqueCode} for question {questionId}");
            }
            else
            {
                _logger.LogError($"StopDisplayCommand failed: {result.GetException()?.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in StopDisplayQuestionForGroup: {ex.Message}");
        }
    }
    else
    {
        _logger.LogWarning($"UniqueCode is empty, command not executed for question {questionId}");
    }
}
```

### 3. Planning.razorの修正

`Planning.razor`の`StopDisplayQuestion`メソッドを以下のように修正する：

```csharp
private async Task StopDisplayQuestion(Guid questionId)
{
    try
    {
        var uniqueCode = groups?.FirstOrDefault(g => g.Id == selectedGroupId)?.UniqueCode ?? "";
        Console.WriteLine($"Stopping display for question {questionId} (UniqueCode: {uniqueCode})...");
        
        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            await JsRuntime.InvokeVoidAsync("alert", "このグループにはUniqueCodeが設定されていません。グループを編集してUniqueCodeを自動生成してください。");
            return;
        }
        
        // 既存のAPI呼び出しは維持しつつ、Hub経由でUniqueCodeを指定した通知も送る
        await QuestionApi.StopDisplayQuestionAsync(questionId);
        await HubService.StopDisplayQuestionForGroup(questionId, uniqueCode);
        Console.WriteLine($"Display stopped for question {questionId} with UniqueCode: {uniqueCode}");
        await RefreshQuestions();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error stopping display: {ex.Message}");
        await JsRuntime.InvokeVoidAsync("alert", $"質問の表示停止に失敗しました: {ex.Message}");
    }
}
```

### 4. HubNotificationService.csについて

`HubNotificationService.cs`は既に`NotifyUniqueCodeGroupAsync`メソッドを持っており、修正は不要。

## 実装の方針

1. 既存のAPIリクエスト（QuestionApi.StopDisplayQuestionAsync）は保持しながら、Hub通知のみをUnique Codeでフィルタリングする
2. 関連するエラーハンドリングを追加し、ユーザーにフィードバックを提供する
3. Console.WriteLineでのログ出力を追加して、デバッグを容易にする

## 期待される結果

この修正により、特定のUnique Codeを持つクライアントグループにのみ表示停止通知が送られるようになり、他のグループのクライアントに影響を与えなくなる。Start DisplayとStop Display両方で一貫した挙動となる。