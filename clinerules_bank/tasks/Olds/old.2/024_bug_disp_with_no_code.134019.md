# モデル: GitHub Copilot

## バグ修正計画: Unique Codeがない場合に質問が表示されないようにする 🔍

### 現在の問題
現在、`EsCQRSQuestions.Web/Components/Pages/Questionair.razor`ではUnique Codeが入力されていない状態でも全ての質問グループの質問が表示されてしまっています。😓 本来の仕様では、Unique Codeが入力された場合のみ、そのグループに関連する質問が表示されるべきです。💡

### 問題の原因調査結果
調査の結果、以下のような原因が特定できました：🕵️‍♀️

1. **クライアント側の問題**：
   - `Questionair.razor`ではUniqueCodeの有無にかかわらず、`RefreshActiveQuestion()`を呼び出して質問を取得しています。🔄
   - `RefreshActiveQuestion()`メソッドは`QuestionApi.GetActiveQuestionAsync(UniqueCode)`を呼び出していますが、UniqueCodeが空でも質問が返ってきた場合はそれを表示しています。⚠️

2. **API側の処理**：
   - `/questions/active`エンドポイント（Program.cs内）では、UniqueCodeが指定されていて、グループIDが見つかり、かつ質問がそのグループに属していない場合は空の結果を返す処理が実装されています。✅
   - しかし、**UniqueCodeが指定されていない場合**にはこのフィルタリングが適用されず、アクティブな質問がそのまま返されてしまいます。⚠️

3. **SignalR接続の動作**：
   - `JoinAsSurveyParticipant`メソッド（QuestionHub.cs）は現在UniqueCodeありの場合のみ実装されていますが、Unique Codeなしでも参加できてしまいます。⚠️

### 修正計画
以下の修正を行うことで、Unique Codeが入力された場合のみ質問が表示されるようにします：🛠️

1. **APIサービス側の修正（Program.cs）**:
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
       
       // 以下は既存のコード...
       var groupService = new EsCQRSQuestions.Domain.Services.QuestionGroupService(executor);
       Guid? groupId = await groupService.GetGroupIdByUniqueCodeAsync(uniqueCode);
       
       // ...
   })
   ```

2. **クライアント側の修正（Questionair.razor）**:
   ```csharp
   private async Task RefreshActiveQuestion()
   {
       try
       {
           Console.WriteLine($"Refreshing active question for UniqueCode: {UniqueCode ?? "none"}");
           
           // UniqueCodeが空の場合は質問を表示しない
           if (string.IsNullOrEmpty(UniqueCode))
           {
               activeQuestion = null;
               return;
           }
           
           activeQuestion = await QuestionApi.GetActiveQuestionAsync(UniqueCode);
           // 残りの処理は同じ...
       }
       catch (Exception ex)
       {
           // エラーハンドリング...
       }
   }
   ```

3. **UIの改善**:
   ```html
   @if (activeQuestion == null)
   {
       <div class="text-center py-5">
           @if (string.IsNullOrEmpty(UniqueCode))
           {
               <h3>アンケートコードを入力してください</h3>
               <p class="lead">アンケートに参加するには、コードが必要です。</p>
           }
           else
           {
               <h3>アンケートへようこそ！</h3>
               <p class="lead">質問が表示されるまでお待ちください。</p>
               <div class="spinner-border text-primary mt-3" role="status">
                   <span class="visually-hidden">Loading...</span>
               </div>
           }
       </div>
   }
   ```

### 修正の利点
1. セキュリティが向上します - Unique Codeを知らない人は質問にアクセスできなくなります。🔐
2. ユーザー体験が向上します - Unique Codeが必要であることが明確になります。✨
3. 本来の設計意図に沿った実装になります。🎯

### 実装における注意点
1. サーバー側とクライアント側の両方で対応が必要です（防御的プログラミング）。🛡️
2. ユーザーに適切なガイダンスメッセージを表示することが重要です。📝
3. 既存のSignalR接続ロジックとの整合性を確保する必要があります。🔄

### 検証計画
1. Unique Codeなしでの接続時に質問が表示されないことを確認します。✅
2. 有効なUnique Codeを入力した場合のみ質問が表示されることを確認します。✅
3. UI上のメッセージが適切に表示されることを確認します。✅