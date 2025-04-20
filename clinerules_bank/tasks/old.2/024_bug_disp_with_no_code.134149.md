# Claude 3 Opus

# 問題：UniqueCodeなしで質問が表示されてしまう問題の調査と解決策

## 現状の問題

現在、`EsCQRSQuestions/EsCQRSQuestions.Web/Components/Pages/Questionair.razor`ページでは、ユーザーがUnique Code（質問グループのコード）を入力していない場合でも、すべてのグループの質問が表示されてしまっています。

要件としては、Unique Codeが入力された場合のみ、そのグループに属する質問を表示し、Unique Codeが入力されていない場合は質問を表示しないようにする必要があります。

## 主要なファイルの分析

### 1. Questionair.razor
- UniqueCodeを入力するためのUI部分は正しく実装されています
- しかし、受け取った質問データをフィルタリングする処理がありません
- アクティブな質問を取得する`RefreshActiveQuestion()`メソッドで、UniqueCodeをAPIに渡していますが、それだけでは不十分です

### 2. QuestionApiClient.cs
- `GetActiveQuestionAsync()`メソッドはUniqueCodeを正しくAPIに渡しています
- UniqueCodeがnullの場合は"/api/questions/active"、指定がある場合は"/api/questions/active?uniqueCode=xxx"というURLでAPIを呼び出しています

### 3. ActiveQuestionQuery.cs
- クエリ自体はUniqueCodeを受け取るようになっていますが、実際のフィルタリング処理はAPIエンドポイント側で行われる想定になっています

### 4. Program.cs（APIエンドポイント）
- `/api/questions/active`エンドポイントでは、UniqueCodeが指定されていて、かつグループIDが見つかった場合にのみフィルタリングを行っています
- UniqueCodeが指定されていない場合は、すべてのアクティブな質問を返しています

## 問題の原因

1. バックエンド側: UniqueCodeが指定されていない場合に、アクティブな質問をすべて返しています。本来は、UniqueCodeが指定されていない場合は空の結果を返すべきです。

2. フロントエンド側: 返ってきた質問データを表示するだけで、UniqueCodeの有無による表示/非表示の制御がありません。

## 修正計画

### バックエンド側の修正 (Program.cs)

```csharp
apiRoute.MapGet("/questions/active", async (
        [FromServices] SekibanOrleansExecutor executor,
        [FromQuery] string? uniqueCode = null) =>
    {
        // UniqueCodeが指定されていない場合は、空の結果を返す
        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            return new ActiveQuestionQuery.ActiveQuestionRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                new List<ActiveQuestionQuery.ResponseRecord>(),
                Guid.Empty);
        }

        // QuestionGroupServiceをその場で生成
        var groupService = new EsCQRSQuestions.Domain.Services.QuestionGroupService(executor);
        
        // UniqueCodeからグループIDを取得
        var groupId = await groupService.GetGroupIdByUniqueCodeAsync(uniqueCode);
        
        // グループIDが見つからない場合も空の結果を返す
        if (!groupId.HasValue)
        {
            return new ActiveQuestionQuery.ActiveQuestionRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                new List<ActiveQuestionQuery.ResponseRecord>(),
                Guid.Empty);
        }
        
        // アクティブな質問を取得
        var activeQuestion = await executor.QueryAsync(new ActiveQuestionQuery(uniqueCode)).UnwrapBox();
        
        // 質問が指定されたグループに属していない場合は空の結果を返す
        if (activeQuestion.QuestionId != Guid.Empty && activeQuestion.QuestionGroupId != groupId.Value)
        {
            return new ActiveQuestionQuery.ActiveQuestionRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                new List<ActiveQuestionQuery.ResponseRecord>(),
                Guid.Empty);
        }
        
        return activeQuestion;
    })
    .WithOpenApi()
    .WithName("GetActiveQuestion");
```

### フロントエンド側の修正 (Questionair.razor)

Questionair.razorファイルも念のため修正を追加します。下記のように`RefreshActiveQuestion()`メソッドを変更して、UniqueCodeが指定されていない場合はアクティブな質問を空に設定します。

```csharp
private async Task RefreshActiveQuestion()
{
    try
    {
        Console.WriteLine($"Refreshing active question for UniqueCode: {UniqueCode ?? "none"}");
        
        // UniqueCodeが指定されていない場合は、アクティブな質問を表示しない
        if (string.IsNullOrWhiteSpace(UniqueCode))
        {
            activeQuestion = null;
            return;
        }
        
        activeQuestion = await QuestionApi.GetActiveQuestionAsync(UniqueCode);
        if (activeQuestion != null && activeQuestion.QuestionId != Guid.Empty)
        {
            Console.WriteLine($"Active question received: {activeQuestion.Text}");
            // Reset submission status if question changed
            var currentResponses = activeQuestion.Responses.Where(r => r.ParticipantName == participantName);
            hasSubmitted = currentResponses.Any();
        }
        else
        {
            Console.WriteLine("No active question at this time");
            activeQuestion = null; // Ensure null if empty result returned
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error refreshing active question: {ex.Message}");
        // Don't update the error message here to avoid overriding connection errors
    }
}
```

## 推奨される修正方針

バックエンド側の修正を優先して行うべきです。バックエンドでUnique Codeなしの場合に空の結果を返すように修正すれば、フロントエンド側の変更は最小限に抑えられます。

しかし、より堅牢な実装のためには、フロントエンド側でもUniqueCodeの有無によるチェックを行い、二重の安全装置を設けることが推奨されます。

バックエンド側の修正を行うことで、システム全体として「Unique Codeが入っていないと質問が表示されない」という要件を満たすことができます。これにより、他のクライアントアプリケーションがAPIを使用する場合にも一貫した動作が保証されます。
