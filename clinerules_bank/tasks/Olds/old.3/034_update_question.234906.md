# GitHub Copilot

## 質問の回答欄更新問題の分析と修正計画

問題：質問の回答で空欄になっているものがあって、それに値を入れて更新しようとしたら、画面が更新されず、データも変わっていなかった。エラーは表示されていなかった。

### 問題の分析

コードを詳細に調査した結果、以下の問題が特定されました：

1. **表示中の質問は更新できない制約**
   - `UpdateQuestionCommand`のハンドラーに、質問が表示中(`IsDisplayed == true`)の場合に更新を拒否する検証が含まれています。
   - `return new InvalidOperationException("Cannot update a question that is currently being displayed");`
   - この制約は妥当ですが、エラーがユーザーに通知されていません。

2. **エラー通知の欠如**
   - QuestionApiClientの`UpdateQuestionAsync`メソッドでは`response.EnsureSuccessStatusCode()`を呼び出していますが、失敗時の詳細なエラー情報を取得していません。
   - Planning.razorの`SaveQuestion`メソッドではエラーをConsoleにのみ出力し、ユーザーに表示していません。

3. **空の回答欄値の処理**
   - 空欄の回答に値を入れる場合の特別な処理がないため、通常の更新処理と同様に扱われています。
   - 更新エラーが発生しても、ユーザーにはフィードバックがなく、データが変更されていないことがわかりません。

### 修正計画

#### 1. QuestionApiClientの改善
```csharp
// UpdateQuestionAsyncメソッドを修正して詳細なエラー情報を返すようにする
public async Task<(bool Success, string? ErrorMessage, object? Result)> UpdateQuestionAsync(
    Guid questionId, 
    string text, 
    List<QuestionOption> options, 
    bool allowMultipleResponses = false,
    CancellationToken cancellationToken = default)
{
    try
    {
        var command = new UpdateQuestionCommand(questionId, text, options, allowMultipleResponses);
        var response = await httpClient.PostAsJsonAsync("/api/questions/update", command, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<object>(cancellationToken) ?? new {};
            return (true, null, result);
        }
        else
        {
            // エラー内容を詳細に取得
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return (false, errorContent, null);
        }
    }
    catch (Exception ex)
    {
        return (false, ex.Message, null);
    }
}
```

#### 2. Planning.razorのSaveQuestionメソッド改善
```csharp
private async Task SaveQuestion()
{
    try
    {
        // グループIDが空の場合はエラーとする - 既存コード
        if (questionModel.QuestionGroupId == Guid.Empty)
        {
            await JsRuntime.InvokeVoidAsync("alert", "質問グループを選択してください。");
            return;
        }
        
        Console.WriteLine("質問を保存中...");
        var options = questionModel.Options.Select(o => new QuestionOption(o.Id, o.Text)).ToList();
        
        var groupIdToUse = questionModel.QuestionGroupId;
        Console.WriteLine($"選択されたグループ: {groupIdToUse}");
        
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
                // エラーメッセージをユーザーに表示
                string errorMsg = "質問の更新に失敗しました";
                if (updateResult.ErrorMessage?.Contains("being displayed") == true)
                {
                    errorMsg = "表示中の質問は編集できません。表示を停止してから再試行してください。";
                }
                else if (!string.IsNullOrEmpty(updateResult.ErrorMessage))
                {
                    errorMsg += $": {updateResult.ErrorMessage}";
                }
                
                await JsRuntime.InvokeVoidAsync("alert", errorMsg);
                Console.Error.WriteLine($"更新エラー: {updateResult.ErrorMessage}");
                return; // エラー発生時は処理を中断
            }
            
            Console.WriteLine("質問が更新されました");
            
            // 残りの既存コード（グループ移動など）
            // ...
        }
        else
        {
            // 新規質問作成 - 既存コード
            // ...
        }
        
        // モーダルを閉じる、データ更新など - 既存コード
        // ...
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"質問保存エラー: {ex.Message}");
        await JsRuntime.InvokeVoidAsync("alert", $"質問の保存中にエラーが発生しました: {ex.Message}");
    }
}
```

#### 3. UpdateQuestionCommandのエラーメッセージ改善
```csharp
// 表示中の質問更新時のエラーメッセージをより明確にする
if (question.IsDisplayed)
{
    return new InvalidOperationException("表示中の質問は更新できません。表示を停止してから編集してください。");
}
```

#### 4. APIエンドポイントのエラーハンドリング強化
Program.csのエンドポイント定義で、適切なエラーレスポンスを返すように改善する。

```csharp
app.MapPost("/api/questions/update", async (
    [FromBody] UpdateQuestionCommand command,
    [FromServices] SekibanOrleansExecutor executor) => 
{
    try
    {
        var result = await executor.CommandAsync(command);
        if (result.IsSuccess)
        {
            return Results.Ok(result.UnwrapBox());
        }
        else
        {
            var exception = result.GetException();
            if (exception != null)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            return Results.BadRequest(new { error = "Unknown error occurred" });
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});
```

### 実装の流れ

1. **QuestionApiClient.cs** の修正
   - `UpdateQuestionAsync`メソッドを詳細なエラー情報を返すように改善

2. **Planning.razor** の修正
   - `SaveQuestion`メソッドでエラーハンドリングを強化し、ユーザーへの通知を追加

3. **UpdateQuestionCommand.cs** の修正
   - エラーメッセージを日本語化してより明確に

4. **Program.cs** の確認と必要に応じた修正
   - エラーハンドリングとレスポンスの改善

### 予想される効果

1. ユーザーは質問更新時のエラーを明確に理解できるようになります（特に「表示中の質問は編集できない」という制約）
2. 空欄に入力した値が更新できないケースでも、理由が明確になります
3. デバッグが容易になり、開発者はより詳細なエラー情報を得られます
4. ユーザー体験が向上し、混乱を減らせます

### テスト計画

1. 表示中の質問を更新しようとし、適切なエラーメッセージが表示されることを確認
2. 空欄の回答フィールドに値を入力して更新し、正常に保存されることを確認
3. 様々なエラー条件（無効な入力など）でのエラーメッセージ表示を確認
4. 正常な更新が適切にUIに反映されることを確認
