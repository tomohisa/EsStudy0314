# GPT-4

# Blazorコンポーネントの修正計画

## 現状の問題点

`EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor`コンポーネントで、Start Display機能を実行すると、質問リストが正しく表示されなくなる問題が発生しています。この問題は、APIクライアントが`waitForSortableUniqueId`パラメータをサポートするように修正されましたが、Razorコンポーネント側でまだその機能を活用していないことが原因と考えられます。

イベント駆動型のプロジェクションでは、コマンド実行後すぐにクエリを実行すると、最新の状態が反映されていない可能性があります。`CommandResponseSimple`から得られる`LastSortableUniqueId`を使って、最新の状態が反映されるまで待機する必要があります。

## 参考実装

`/Users/tomohisa/dev/GitHub/Sekiban/templates/Sekiban.Pure.Templates/content/Sekiban.Orleans.Aspire/OrleansSekiban.Web/Components/Pages/Weather.razor`を参考にして、以下のパターンが確認できました：

1. コマンド実行後、そのレスポンスから`LastSortableUniqueId`を取得
2. 次のクエリ実行時に`waitForSortableUniqueId`パラメータとして渡す
3. これにより、コマンドによる変更が確実に反映された後のデータを取得できる

例：
```csharp
var response = await WeatherApi.UpdateLocationAsync(
    editLocationModel.WeatherForecastId,
    editLocationModel.NewLocation!);
forecasts = await WeatherApi.GetWeatherAsync(waitForSortableUniqueId: response.LastSortableUniqueId);
```

## 修正が必要な箇所

`Planning.razor`で以下のメソッドを修正する必要があります：

1. `StartDisplayQuestion`
2. `StopDisplayQuestion`
3. `SaveQuestion`
4. `SaveGroup`
5. `DeleteQuestion`
6. `DeleteGroup`
7. その他のコマンド実行後にデータをリフレッシュしている箇所

## 修正の具体的内容

### 1. StartDisplayQuestion メソッドの修正

```csharp
private async Task StartDisplayQuestion(Guid questionId)
{
    try
    {
        var uniqueCode = groups?.FirstOrDefault(g => g.Id == selectedGroupId)?.UniqueCode ?? "";
        Console.WriteLine($"Starting display for question {questionId} (UniqueCode: {uniqueCode})...");
        
        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            await JsRuntime.InvokeVoidAsync("alert", "このグループにはUniqueCodeが設定されていません。グループを編集してUniqueCodeを自動生成してください。");
            return;
        }
        
        // コマンド実行結果を取得してSortableUniqueIdを利用する
        var response = await QuestionApi.StartDisplayQuestionAsync(questionId);
        Console.WriteLine($"Display started for question {questionId} with UniqueCode: {uniqueCode}");
        
        // SortableUniqueIdを使ってデータをリフレッシュ
        await RefreshQuestionsWithSortableUniqueId(response.LastSortableUniqueId);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error starting display: {ex.Message}");
        await JsRuntime.InvokeVoidAsync("alert", $"質問の表示に失敗しました: {ex.Message}");
    }
}
```

### 2. StopDisplayQuestion メソッドの修正

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
        
        // コマンド実行結果を取得してSortableUniqueIdを利用する
        var response = await QuestionApi.StopDisplayQuestionAsync(questionId);
        Console.WriteLine($"Display stopped for question {questionId} with UniqueCode: {uniqueCode}");
        
        // SortableUniqueIdを使ってデータをリフレッシュ
        await RefreshQuestionsWithSortableUniqueId(response.LastSortableUniqueId);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error stopping display: {ex.Message}");
        await JsRuntime.InvokeVoidAsync("alert", $"質問の表示停止に失敗しました: {ex.Message}");
    }
}
```

### 3. RefreshQuestionsWithSortableUniqueId メソッドの追加

```csharp
private async Task RefreshQuestionsWithSortableUniqueId(string sortableUniqueId)
{
    try
    {
        Console.WriteLine($"Refreshing questions with SortableUniqueId: {sortableUniqueId}...");
        // 旧APIを使用して互換性を維持
        var fetchedQuestions = await QuestionApi.GetQuestionsAsync(waitForSortableUniqueId: sortableUniqueId);
        questions = fetchedQuestions.ToList();
        
        // 新しいMultiProjectorを使用したAPIも呼び出す
        var fetchedQuestionsWithGroupInfo = await QuestionApi.GetQuestionsWithGroupInfoAsync(
            textContains: "", 
            waitForSortableUniqueId: sortableUniqueId);
        questionsWithGroupInfo = fetchedQuestionsWithGroupInfo.ToList();
        
        Console.WriteLine($"Fetched {questions.Count} questions and {questionsWithGroupInfo.Count} questions with group info");
        await RefreshQuestionsInGroupWithSortableUniqueId(sortableUniqueId);
        await InvokeAsync(() => StateHasChanged());
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error refreshing questions: {ex.Message}");
    }
}
```

### 4. RefreshQuestionsInGroupWithSortableUniqueId メソッドの追加

```csharp
private async Task RefreshQuestionsInGroupWithSortableUniqueId(string sortableUniqueId)
{
    if (selectedGroupId.HasValue)
    {
        try
        {
            Console.WriteLine($"Refreshing questions in group {selectedGroupId.Value} with SortableUniqueId: {sortableUniqueId}...");
            
            var fetchedQuestionsInGroup = await QuestionApi.GetQuestionsByGroupAsync(
                selectedGroupId.Value,
                textContains: "",
                waitForSortableUniqueId: sortableUniqueId);
                
            questionsInGroup = fetchedQuestionsInGroup.ToList();
            Console.WriteLine($"Fetched {questionsInGroup.Count} questions in group");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error refreshing questions in group: {ex.Message}");
            var groupId = selectedGroupId.Value;
            
            if (questionsWithGroupInfo is not null)
            {
                // Filter questions by the selected group ID
                questionsInGroup = questionsWithGroupInfo
                    .Where(q => q.QuestionGroupId == groupId)
                    .ToList();
            }
            else
            {
                questionsInGroup = new List<QuestionsQuery.QuestionDetailRecord>();
            }
        }
    }
    else
    {
        questionsInGroup = null;
    }
}
```

### 5. ViewQuestionDetails メソッドの修正

```csharp
private async Task ViewQuestionDetails(Guid questionId, string sortableUniqueId = null)
{
    try
    {
        Console.WriteLine($"Viewing question details for {questionId}...");
        // 既存のselectedQuestionをnullに設定し、UIを更新してから新しいデータを取得
        selectedQuestion = null;
        StateHasChanged(); // 一度UIを更新
        
        // データ取得を待機
        await Task.Delay(50); // 少しの遅延を入れて、UIの更新が確実に行われるようにする
        
        // 新しいデータを取得（SortableUniqueIdがあれば使用）
        selectedQuestion = await QuestionApi.GetQuestionByIdAsync(
            questionId,
            waitForSortableUniqueId: sortableUniqueId);
        Console.WriteLine("Question details loaded");
        
        // UIを最終更新
        StateHasChanged();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error viewing question details: {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
    }
}
```

### 6. SaveQuestion メソッドの修正

修正が必要な箇所が多いため、主要な変更部分のみ記載します。コマンド実行後のリフレッシュ処理を修正します：

```csharp
if (isEditMode && editQuestionId.HasValue)
{
    // まず質問自体を更新（複数回答フラグも含める）
    var updateResult = await QuestionApi.UpdateQuestionAsync(
        editQuestionId.Value, 
        questionModel.Text, 
        options, 
        questionModel.AllowMultipleResponses);
        
    if (!updateResult.Success)
    {
        // エラー処理（変更なし）
    }
    else
    {
        Console.WriteLine("質問が更新されました");
        // SortableUniqueIdを使ってデータをリフレッシュ
        await RefreshQuestionsWithSortableUniqueId(updateResult.Result?.LastSortableUniqueId);
        
        // 以下、グループ変更処理...
    }
}
else
{
    // 新規質問作成
    try {
        var response = await QuestionApi.CreateQuestionWithGroupAsync(
            questionModel.Text, 
            options, 
            groupIdToUse, 
            questionModel.AllowMultipleResponses);
        Console.WriteLine($"質問をグループ {groupIdToUse} に作成しました");
        
        // SortableUniqueIdを使ってデータをリフレッシュ
        await RefreshQuestionsWithSortableUniqueId(response.LastSortableUniqueId);
    } catch (Exception ex) {
        // エラー処理（変更なし）
    }
}
```

## 実装ステップ

1. `RefreshQuestionsWithSortableUniqueId` と `RefreshQuestionsInGroupWithSortableUniqueId` メソッドを追加
2. `StartDisplayQuestion` と `StopDisplayQuestion` メソッドを修正
3. `SaveQuestion` と `SaveGroup` メソッドのコマンド実行後のリフレッシュ処理を修正
4. `DeleteQuestion` と `DeleteGroup` メソッドのコマンド実行後のリフレッシュ処理を修正
5. 必要に応じて `ViewQuestionDetails` のようなクエリメソッドも修正

## 考慮事項

1. 既存の `RefreshQuestions` と `RefreshQuestionsInGroup` メソッドはそのまま残し、SortableUniqueIdが必要な場合に新しいメソッドを使用
2. `StartDisplayQuestion` メソッドは `HubService` を通じてコマンドを実行しているため、API直接呼び出しに変更
3. SignalR通知との連携も考慮する必要がある（SignalRとwaitForSortableUniqueId両方の更新メカニズムを適切に組み合わせる）
4. すべてのコマンド操作後に、最新状態を反映するためにSortableUniqueIdを利用する一貫したパターンを実装

## 変更対象ファイル

- EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor
  - メソッドの追加と修正

## 期待される結果

1. `Start Display` 機能実行後、最新の状態が正しく表示されるようになる
2. 他のコマンド操作後も同様に最新状態が反映される
3. イベント駆動プロジェクションの特性を考慮した適切な実装となる
