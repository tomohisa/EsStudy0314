# Cline

## 問題点の解析と計画：「Start Display」でイベントが保存・表示されない問題

QuestionGroupとQuestion作成後、「Start Display」ボタンを押したときにPostgreSQLにイベントが保存されず、クライアント側に質問が表示されない問題について調査しました。

### 現状の問題点

関連コードを分析した結果、以下の問題が特定されました：

1. **イベント永続化の欠如**: 
   - `QuestionHub.cs`の`StartDisplayQuestionForGroup`メソッドは単に通知を送信するだけで、PostgreSQLにイベントを永続化するコマンドを実行していません。
   - SignalRを介して通知は送られていますが、実際のドメインイベントが発生していません。

2. **コマンド実行の欠如**:
   - クライアント→サーバーの通信フローは機能していますが、サーバー側で`StartDisplayQuestion`のようなコマンドを実行する部分が欠けています。

3. **通知とデータの不一致**:
   - SignalR通知は送信されているかもしれませんが、データベースに状態が保存されていないため、データの永続化と通知が同期していません。

### 修正計画

1. **ドメインコマンドの確認と実装**:
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Commands` 名前空間内に`StartDisplayQuestionCommand`があるか確認
   - 存在しない場合、適切なコマンドを作成
   - コマンドハンドラーが正しく実装されているか確認

2. **QuestionHub.csの修正**:
   ```csharp
   public async Task StartDisplayQuestionForGroup(Guid questionId, string uniqueCode)
   {
       _logger.LogInformation($"StartDisplayQuestionForGroup called: QuestionId={questionId}, UniqueCode={uniqueCode}");
       
       if (!string.IsNullOrWhiteSpace(uniqueCode))
       {
           try
           {
               // ここにコマンド実行を追加
               await _executor.CommandAsync(new StartDisplayQuestionCommand(questionId));
               
               // 既存の通知コード
               await _notificationService.NotifyUniqueCodeGroupAsync(
                   uniqueCode, 
                   "QuestionDisplayStarted", 
                   new { QuestionId = questionId });
               
               _logger.LogInformation($"Display command executed and notification sent for question {questionId}");
           }
           catch (Exception ex)
           {
               _logger.LogError($"Error starting display: {ex.Message}");
           }
       }
       else
       {
           _logger.LogWarning($"UniqueCode is empty, command not executed for question {questionId}");
       }
   }
   ```

3. **StartDisplayQuestionCommandの確認と修正**:
   - コマンドが正しく質問の状態を更新するか確認
   - イベントハンドラーが正しくイベントを生成するか確認
   - プロジェクターが正しく状態を更新するか確認

4. **イベント処理のデバッグ**:
   - ログレベルを`Debug`に設定し、イベント処理の流れを追跡
   - PostgreSQLにイベントが書き込まれていることを確認するログを追加

### 調査が必要なファイル

以下のファイルを追加で調査する必要があります：

1. **ドメイン関連**:
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Commands.StartDisplayQuestionCommand`
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionDisplayStarted`
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Projectors.QuestionProjector`
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Payloads.Question`

2. **API Service関連**:
   - `EsCQRSQuestions.ApiService.Program.cs` (エンドポイント設定)
   - `EsCQRSQuestions.ApiService.QuestionController` (APIエンドポイント)

3. **クライアント関連**:
   - `EsCQRSQuestions.AdminWeb.QuestionApiClient.cs` (クライアントからの呼び出し)

### 実装計画のステップ

1. まず、ドメインコマンドとイベントを確認
2. QuestionHub.csにコマンド実行コードを追加
3. ログ出力を強化してデバッグ
4. 修正後のフロー全体をテスト

この修正で、「Start Display」ボタンを押したときに正しくイベントが永続化され、クライアント側に質問が表示されるようになるはずです。
