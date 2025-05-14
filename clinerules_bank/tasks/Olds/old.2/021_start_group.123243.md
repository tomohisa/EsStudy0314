# Cline

## 問題点の解析と計画：「Start Display」でイベントが保存・表示されない問題

QuestionGroupとQuestion作成後、「Start Display」ボタンを押したときにPostgreSQLにイベントが保存されず、クライアント側に質問が表示されない問題について調査しました。

### 現状の問題点と解決方法の特定

コードを詳細に調査した結果、以下の問題が特定されました：

1. **コマンド実行と通知の不整合**: 
   - `QuestionHub.cs`の`StartDisplayQuestionForGroup`メソッドは単にSignalR通知を送信するだけで、`StartDisplayCommand`を実行していません。
   - 一方、`Program.cs`の262-263行目には正しいコマンド実行のエンドポイントが定義されています。

2. **実装の流れ**: 
   ```csharp
   // Program.cs (Line 262-263)
   apiRoute
       .MapPost(
           "/questions/startDisplay",
           async (
               [FromBody] StartDisplayCommand command,
               [FromServices] SekibanOrleansExecutor executor) => 
           {
               // ワークフローを使って排他制御を実装
               var workflow = new QuestionDisplayWorkflow(executor);
               return await workflow.StartDisplayQuestionExclusivelyAsync(command.QuestionId).UnwrapBox();
           })
   ```

   - 上記のエンドポイントは正しく実装されており、`StartDisplayCommand`を実行し、イベントをPostgreSQLに保存します。
   - また、`QuestionDisplayWorkflow`も正しく実装されています。

3. **QuestionHub.csの問題点**:
   ```csharp
   // QuestionHub.cs
   public async Task StartDisplayQuestionForGroup(Guid questionId, string uniqueCode)
   {
       _logger.LogInformation($"StartDisplayQuestionForGroup called: QuestionId={questionId}, UniqueCode={uniqueCode}");
       
       if (!string.IsNullOrWhiteSpace(uniqueCode))
       {
           try
           {
               // ここにStartDisplayCommandの実行が必要
               // 以下の通知コードはそのまま

               await _notificationService.NotifyUniqueCodeGroupAsync(
                   uniqueCode, 
                   "QuestionDisplayStarted", 
                   new { QuestionId = questionId });
           }
           catch (Exception ex)
           {
               _logger.LogError($"Error notifying group: {ex.Message}");
           }
       }
   }
   ```

### 修正計画

以下の修正を`QuestionHub.cs`に適用することで問題を解決できます：

```csharp
public async Task StartDisplayQuestionForGroup(Guid questionId, string uniqueCode)
{
    _logger.LogInformation($"StartDisplayQuestionForGroup called: QuestionId={questionId}, UniqueCode={uniqueCode}");
    
    if (!string.IsNullOrWhiteSpace(uniqueCode))
    {
        try
        {
            // 1. まずワークフローを使ってコマンドを実行し、イベントを保存
            var workflow = new QuestionDisplayWorkflow(_executor);
            var result = await workflow.StartDisplayQuestionExclusivelyAsync(questionId);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation($"StartDisplayCommand executed successfully for question {questionId}");
                
                // 2. 実行成功した場合のみ、通知を送信
                await _notificationService.NotifyUniqueCodeGroupAsync(
                    uniqueCode, 
                    "QuestionDisplayStarted", 
                    new { QuestionId = questionId });
                
                _logger.LogInformation($"Notification sent to group {uniqueCode} for question {questionId}");
            }
            else
            {
                _logger.LogError($"StartDisplayCommand failed: {result.GetException()?.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in StartDisplayQuestionForGroup: {ex.Message}");
        }
    }
    else
    {
        _logger.LogWarning($"UniqueCode is empty, command not executed for question {questionId}");
    }
}
```

### 必要な変更

1. **QuestionHub.csへのインポート追加**:
   ```csharp
   using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
   using EsCQRSQuestions.Domain.Workflows;
   ```

2. **QuestionHub.csのコード変更**:
   - `StartDisplayQuestionForGroup`メソッドを上記のように修正
   - `QuestionDisplayWorkflow`を使ってコマンドを実行するように変更

### 確認事項

修正後、以下の処理フローが正しく実行されることを確認します：

1. 管理画面で「Start Display」ボタンを押す
2. AdminWebから`QuestionHubService.StartDisplayQuestionForGroup`が呼び出される
3. APIサービスの`QuestionHub.StartDisplayQuestionForGroup`が実行される
4. `QuestionDisplayWorkflow.StartDisplayQuestionExclusivelyAsync`が実行される
5. `StartDisplayCommand`が実行され、イベントがPostgreSQLに保存される
6. UniqueCodeグループに通知が送信される
7. クライアント側でQuestionairの表示が更新される

この修正により、「Start Display」ボタンを押したときに正しくイベントが永続化され、クライアント側に質問が表示されるようになります。
