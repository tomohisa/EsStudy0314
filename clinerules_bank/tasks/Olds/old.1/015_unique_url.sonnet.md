# UniqueURL機能の設計

clinerules_bank/tasks/014_unique_id.md の続きとして、管理者画面から回答用のリンクを提供する機能を設計します。前回実装したQuestionGroupのUniqueCodeを活用し、URLパラメータを使って特定のグループに対する回答ページを表示します。

## 1. Questionair.razorにURLパラメータを追加

現在の実装では、回答ページは固定のパス `/questionair` で表示されています。これをURLパラメータを受け取れるように変更します。

**変更前:**
```csharp
@page "/questionair"
```

**変更後:**
```csharp
@page "/questionair"
@page "/questionair/{GroupCode}"
```

GroupCodeパラメータを受け取るためのプロパティを追加します：

```csharp
[Parameter]
public string? GroupCode { get; set; }
```

## 2. URLパラメータに基づく処理の実装

`OnInitializedAsync`メソッドを拡張し、GroupCodeパラメータがある場合の処理を追加します：

```csharp
protected override async Task OnInitializedAsync()
{
    // 既存の初期化コード（SignalRの設定など）
    
    // GroupCodeがある場合、そのグループの質問を取得
    if (!string.IsNullOrEmpty(GroupCode))
    {
        try
        {
            // GroupCodeに対応するQuestionGroupの情報を取得
            var groupQuestions = await QuestionApi.GetQuestionsByGroupCodeAsync(GroupCode);
            // 取得した質問を表示するためのロジック
            // ...
        }
        catch (Exception ex)
        {
            errorMessage = $"グループの読み込み中にエラーが発生しました: {ex.Message}";
        }
    }
    else
    {
        // 通常のアクティブな質問を取得する既存の処理
        await RefreshActiveQuestion();
    }
}
```

## 3. QuestionApiClientの拡張

`QuestionApiClient`クラスに、GroupCodeを使って質問を取得するメソッドを追加します：

```csharp
public async Task<List<QuestionsQuery.QuestionDetailRecord>> GetQuestionsByGroupCodeAsync(
    string groupCode,
    CancellationToken cancellationToken = default)
{
    var questions = await httpClient.GetFromJsonAsync<List<QuestionsQuery.QuestionDetailRecord>>(
        $"/api/questions/byGroupCode/{groupCode}", 
        cancellationToken);
    
    return questions ?? new List<QuestionsQuery.QuestionDetailRecord>();
}
```

## 4. APIサービスへのエンドポイント追加

`EsCQRSQuestions.ApiService`の`Program.cs`に新しいエンドポイントを追加します：

```csharp
// GroupCodeを使って質問を取得するエンドポイント
questionApiGroup.MapGet("/byGroupCode/{groupCode}", 
    async (string groupCode, SekibanOrleansExecutor executor) => 
    {
        var result = await executor.QueryAsync(new GetQuestionsByGroupCodeQuery(groupCode))
            .UnwrapBox();
        return Results.Ok(result);
    });
```

## 5. 新しいクエリの実装

`GetQuestionsByGroupCodeQuery`を`EsCQRSQuestions.Domain`プロジェクトに追加します：

```csharp
[GenerateSerializer]
public record GetQuestionsByGroupCodeQuery(string GroupCode) 
    : IMultiProjectionQuery<AggregateListProjector<QuestionGroupProjector>, GetQuestionsByGroupCodeQuery, List<QuestionsQuery.QuestionDetailRecord>>
{
    public static ResultBox<List<QuestionsQuery.QuestionDetailRecord>> HandleQuery(
        MultiProjectionState<AggregateListProjector<QuestionGroupProjector>> projectionState,
        GetQuestionsByGroupCodeQuery query,
        IQueryContext context)
    {
        // GroupCodeを使ってQuestionGroupを検索
        var group = projectionState.Payload.Aggregates.Values
            .Where(a => a.Payload is QuestionGroup)
            .Select(a => (QuestionGroup)a.Payload)
            .FirstOrDefault(g => g.UniqueCode == query.GroupCode);

        if (group == null)
        {
            return new List<QuestionsQuery.QuestionDetailRecord>();
        }

        // QuestionGroupに所属する質問の詳細を取得
        var questionIds = group.Questions.Select(q => q.QuestionId).ToList();
        
        // 質問の詳細情報を取得するクエリを実行
        var questions = new List<QuestionsQuery.QuestionDetailRecord>();
        foreach (var questionId in questionIds)
        {
            var questionResult = context.ExecuteQuery(new QuestionsQuery(questionId));
            if (questionResult.IsSuccess)
            {
                questions.Add(questionResult.GetValue());
            }
        }

        return questions.OrderBy(q => group.Questions
            .First(ref => ref.QuestionId == q.QuestionId).Order).ToList();
    }
}
```

## 6. Planning.razorに回答用リンクを追加

管理者画面の`GroupQuestionsList`コンポーネントにリンク生成機能を追加します。

`GroupQuestionsList.razor`に以下のボタンを追加：

```html
<button class="btn btn-sm btn-outline-info" 
        @onclick="() => CopyQuestionnaireLink(GroupName, UniqueCode)" 
        title="回答リンクをコピー">
    <i class="bi bi-link"></i> 回答用リンク
</button>
```

`Planning.razor`に以下のメソッドを追加：

```csharp
private async Task CopyQuestionnaireLink(string groupName, string uniqueCode)
{
    try
    {
        // ベースURLの取得（Aspireの設定から）
        // .NET Aspireでは、サービスの公開URLを取得するためにConfiguration経由で設定を取得
        var webBaseUrl = "https://localhost:7201"; // デフォルト値
                
        // 実際のURLを作成
        var questionnaireUrl = $"{webBaseUrl}/questionair/{uniqueCode}";
        
        // URLをクリップボードにコピー
        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", questionnaireUrl);
        
        // 成功メッセージを表示
        await JsRuntime.InvokeVoidAsync("alert", $"「{groupName}」の回答用リンクをクリップボードにコピーしました。");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"リンクのコピー中にエラーが発生しました: {ex.Message}");
        await JsRuntime.InvokeVoidAsync("alert", "リンクの生成中にエラーが発生しました。");
    }
}
```

## 7. ベースURLの設定

.NET Aspireの構成からWebフロントエンドのURLを取得するために、以下の方法を検討します：

### 7.1 Configuration経由での設定取得

`EsCQRSQuestions.AdminWeb/appsettings.json`に設定を追加：

```json
{
  "AppSettings": {
    "WebFrontendBaseUrl": "https://localhost:7201"
  }
}
```

そして、DIでIConfigurationを注入して設定を取得：

```csharp
@inject IConfiguration Configuration

// メソッド内で
var webBaseUrl = Configuration["AppSettings:WebFrontendBaseUrl"] ?? "https://localhost:7201";
```

### 7.2 環境変数経由での取得

運用環境では環境変数経由で設定：

```csharp
var webBaseUrl = Environment.GetEnvironmentVariable("WEB_FRONTEND_URL") ?? "https://localhost:7201";
```

## 8. 回答ページの表示ロジック

URLパラメータで特定のグループが指定された場合、そのグループに関連する質問を順番に表示するUIを実装します：

```csharp
@if (!string.IsNullOrEmpty(GroupCode) && groupQuestions != null && groupQuestions.Any())
{
    <div class="group-questions-container">
        <h3>@currentGroupName</h3>
        
        @if (currentQuestionIndex < groupQuestions.Count)
        {
            var currentQuestion = groupQuestions[currentQuestionIndex];
            
            <div class="question-container">
                <h4>質問 @(currentQuestionIndex + 1) / @groupQuestions.Count</h4>
                <h3 class="mb-4">@currentQuestion.Text</h3>
                
                <!-- 選択肢のUI（既存のコードと類似） -->
                
                <!-- ナビゲーションボタン -->
                <div class="d-flex justify-content-between mt-4">
                    <button class="btn btn-secondary" @onclick="PreviousQuestion" 
                            disabled="@(currentQuestionIndex == 0)">前へ</button>
                    <button class="btn btn-primary" @onclick="NextQuestion"
                            disabled="@(currentQuestionIndex == groupQuestions.Count - 1)">次へ</button>
                </div>
            </div>
        }
        else
        {
            <div class="completed-message">
                <h3>すべての質問に回答しました</h3>
                <p>ご回答ありがとうございました。</p>
            </div>
        }
    </div>
}
```

## 実装の流れ

1. `QuestionApiClient`に新しいメソッドを追加
2. APIサービスに新しいエンドポイントを追加
3. `GetQuestionsByGroupCodeQuery`を実装
4. `Questionair.razor`にURLパラメータ対応を追加
5. `Planning.razor`に回答用リンク生成機能を追加
6. ベースURLの設定方法を確立
7. 回答ページの表示ロジックを実装

この設計により、管理者が作成した質問グループに対して、直接リンクを共有することができ、回答者はその特定のグループに紐づく質問に回答できるようになります。
