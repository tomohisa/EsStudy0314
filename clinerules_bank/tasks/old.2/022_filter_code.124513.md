# Cline

## 問題分析と解決計画：異なるグループ間で質問表示が混在する問題

2つの異なるグループを作成し、AdminWebからStart Displayボタンを押すと、両方のグループに同じ質問が表示されてしまう問題について調査しました。

### 現状の問題点

関連コードを詳細に分析した結果、以下の問題が特定されました：

1. **SignalR通知は正しく実装されている**：
   - `QuestionHub.cs`の`StartDisplayQuestionForGroup`メソッドは、指定されたUniqueCodeグループにのみ通知を送信しており、この部分は正しく動作しています。
   - `HubNotificationService`の`NotifyUniqueCodeGroupAsync`メソッドも特定のグループにのみ通知を送信します。

2. **API呼び出しでフィルタリングがない**：
   - 問題の根本原因は`Questionair.razor`の`RefreshActiveQuestion`メソッドにあります。
   - このメソッドは、SignalR通知を受け取った後に`QuestionApiClient.GetActiveQuestionAsync()`を呼び出してアクティブな質問を取得しています。
   - しかし、この`GetActiveQuestionAsync()`メソッドはUniqueCodeによるフィルタリングを行っていません。
   - そのため、どのグループから`Start Display`が押されても、すべてのクライアントが同じアクティブ質問を取得してしまいます。

3. **`ActiveQuestionQuery`クエリにフィルタリングがない**：
   - サーバー側の`/api/questions/active`エンドポイントもUniqueCodeによるフィルタリングをサポートしていません。

### 解決策

以下の修正を行うことで問題を解決できます：

1. **`ActiveQuestionQuery`クラスの修正**：
   ```csharp
   // 現在の実装
   public record ActiveQuestionQuery() : IQuery<ActiveQuestionQuery, ActiveQuestionRecord>;
   
   // 新しい実装
   public record ActiveQuestionQuery(string? UniqueCode = null) : IQuery<ActiveQuestionQuery, ActiveQuestionRecord>;
   ```

2. **質問プロジェクターの修正**：
   - `ActiveQuestionQuery`ハンドラーを更新して、UniqueCodeパラメータを考慮するようにします。
   - 各質問には、表示するためのUniqueCodeを保存するフィールドを追加します。

3. **`StartDisplayCommand`/イベントの拡張**：
   ```csharp
   // 現在の実装
   public record StartDisplayCommand(Guid QuestionId) : ICommand;
   
   // 新しい実装
   public record StartDisplayCommand(Guid QuestionId, string UniqueCode) : ICommand;
   ```

4. **`QuestionHub`の変更**：
   ```csharp
   public async Task StartDisplayQuestionForGroup(Guid questionId, string uniqueCode)
   {
       // ...
       var workflow = new QuestionDisplayWorkflow(_executor);
       // StartDisplayCommandにUniqueCodeを渡す
       var result = await workflow.StartDisplayQuestionExclusivelyAsync(questionId, uniqueCode); 
       // ...
   }
   ```

5. **APIクライアントの修正**：
   ```csharp
   // Web側のQuestionApiClient.cs
   public async Task<ActiveQuestionQuery.ActiveQuestionRecord?> GetActiveQuestionAsync(
       string? uniqueCode = null, CancellationToken cancellationToken = default)
   {
       string url = uniqueCode == null 
           ? "/api/questions/active" 
           : $"/api/questions/active?uniqueCode={Uri.EscapeDataString(uniqueCode)}";
           
       return await httpClient.GetFromJsonAsync<ActiveQuestionQuery.ActiveQuestionRecord?>(url, cancellationToken);
   }
   ```

6. **`Questionair.razor`の修正**：
   ```csharp
   private async Task RefreshActiveQuestion()
   {
       try
       {
           Console.WriteLine($"Refreshing active question for UniqueCode: {UniqueCode ?? "none"}");
           // UniqueCodeを渡してフィルタリングされた質問を取得
           activeQuestion = await QuestionApi.GetActiveQuestionAsync(UniqueCode);
           // ...
       }
       catch (Exception ex)
       {
           // ...
       }
   }
   ```

7. **エンドポイントの修正**：
   ```csharp
   // Program.cs内のエンドポイント定義
   apiRoute.MapGet("/questions/active", async (
       [FromServices] SekibanOrleansExecutor executor,
       [FromQuery] string? uniqueCode) =>
   {
       var activeQuestion = await executor.QueryAsync(new ActiveQuestionQuery(uniqueCode)).UnwrapBox();
       return activeQuestion;
   });
   ```

### 必要な変更ファイル

1. **ドメイン関連**：
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Commands.StartDisplayCommand`
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionDisplayStarted`
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Queries.ActiveQuestionQuery`
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Payloads.Question` (UniqueCodeフィールド追加)

2. **APIサービス関連**：
   - `EsCQRSQuestions.ApiService.Program.cs` (エンドポイント修正)
   - `EsCQRSQuestions.ApiService.QuestionHub.cs` (既に修正済み)
   - `EsCQRSQuestions.Domain.Workflows.QuestionDisplayWorkflow.cs`

3. **クライアント関連**：
   - `EsCQRSQuestions.Web.QuestionApiClient.cs` (GetActiveQuestionAsync修正)
   - `EsCQRSQuestions.Web.Components.Pages.Questionair.razor` (RefreshActiveQuestion修正)
   - `EsCQRSQuestions.AdminWeb.QuestionApiClient.cs` (StartDisplayQuestionAsync修正)
   - `EsCQRSQuestions.AdminWeb.Components.Pages.Planning.razor` (StartDisplayQuestion呼び出し修正)

### 実装計画

1. まずドメインモデルの修正：
   - StartDisplayCommandとイベントにUniqueCodeパラメータを追加
   - ActiveQuestionQueryにUniqueCodeパラメータを追加
   - Question集約にUniqueCodeフィールドを追加

2. ワークフロー・サービスの修正：
   - QuestionDisplayWorkflowの修正
   - API エンドポイントの修正

3. クライアント側の修正：
   - WebとAdminWebのQuestionApiClient修正
   - Questionair.razorの更新

4. 全体のテスト：
   - 2つの異なるUniqueCodeを持つグループを作成
   - それぞれのグループで質問を作成
   - 一方のグループでStart Displayを実行
   - 両方のグループクライアントで確認し、正しいグループのみに質問が表示されることを確認

この修正により、異なるグループ間で質問表示が混在する問題を解決できます。
