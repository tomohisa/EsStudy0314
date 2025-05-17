# モデル: GitHub Copilot

# 存在しないアンケートコードのエラー表示機能の設計

## 問題の概要

現在のアンケートシステムでは、ユーザーが存在しないアンケートコードを入力しても、エラーメッセージが表示されず、正常に動作しているように見えてしまいます。これによりユーザーは無限に待たされることになり、ユーザー体験が悪化しています。😞

## 現状の実装分析

### 現在の動作
1. ユーザーがアンケートコードを入力して「アンケートに参加」ボタンをクリック
2. `NavigateToSurvey()`メソッドが呼び出され、URLが更新される
3. SignalRグループに参加し、`RefreshActiveQuestion()`が呼び出される
4. `QuestionApi.GetActiveQuestionAsync(UniqueCode)`でアンケートの質問を取得
5. **問題点**: 存在しないコードの場合、API呼び出しが失敗しても、特にエラーメッセージが表示されない

### 問題のある箇所

1. `NavigateToSurvey()`メソッド内で、コードの存在確認を行っていない
2. `RefreshActiveQuestion()`メソッド内で例外が発生した場合、エラーメッセージが更新されない
3. APIクライアント側で存在しないコードを明示的にチェックする仕組みがない

## 解決策の設計

### 1. QuestionApiClientの拡張：コード存在確認メソッドの追加

```csharp
// QuestionApiClient.cs に追加
public async Task<bool> ValidateUniqueCodeAsync(string uniqueCode, CancellationToken cancellationToken = default)
{
    try
    {
        string url = $"/api/questions/validate/{uniqueCode}";
        var response = await httpClient.GetAsync(url, cancellationToken);
        
        // ステータスコードで判断（404の場合はコードが存在しない）
        return response.IsSuccessStatusCode;
    }
    catch (Exception)
    {
        // 例外が発生した場合は無効と判断
        return false;
    }
}
```

### 2. バックエンドAPIの拡張：コード検証エンドポイントの追加

```csharp
// EsCQRSQuestions.ApiService/Program.cs に追加
app.MapGet("/api/questions/validate/{uniqueCode}", async (string uniqueCode, [FromServices] SekibanOrleansExecutor executor) =>
{
    // グループIDが存在するかどうかを確認するためのクエリを実行
    var groupExists = await executor.QueryAsync(new QuestionGroupExistsQuery(uniqueCode));
    
    if (groupExists.IsSuccess && groupExists.GetValue())
    {
        return Results.Ok();
    }
    
    return Results.NotFound();
});
```

### 3. Questionair.razorの修正：ナビゲーション前にコード検証を行う

```csharp
/// <summary>
/// アンケートコードを入力してナビゲーションするメソッド
/// </summary>
private async Task NavigateToSurvey()
{
    if (!string.IsNullOrWhiteSpace(inputUniqueCode))
    {
        // URLに含まれない文字を削除/置換
        var sanitizedCode = Uri.EscapeDataString(inputUniqueCode.Trim());
        
        // コードが有効かどうかを検証
        isValidating = true;
        errorMessage = ""; // エラーメッセージをクリア
        
        try
        {
            bool isValidCode = await QuestionApi.ValidateUniqueCodeAsync(sanitizedCode);
            
            if (!isValidCode)
            {
                errorMessage = "入力されたアンケートコードは存在しません。正しいコードを入力してください。";
                isValidating = false;
                return;
            }
            
            // UniqueCodeを更新
            UniqueCode = sanitizedCode;
            
            // URLを更新（クライアントサイドルーティング）
            NavigationManager.NavigateTo($"/questionair/{sanitizedCode}");
            
            // ナビゲーション後にSignalRグループに参加
            if (hubConnection.State == HubConnectionState.Connected)
            {
                try
                {
                    Console.WriteLine($"Joining survey with unique code: {sanitizedCode} after navigation");
                    await hubConnection.InvokeAsync("JoinAsSurveyParticipant", sanitizedCode);
                    await RefreshActiveQuestion();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error joining survey after navigation: {ex.Message}");
                    errorMessage = "アンケートグループへの参加中にエラーが発生しました。ページを更新してください。";
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"アンケートコードの検証中にエラーが発生しました: {ex.Message}";
            Console.Error.WriteLine(errorMessage);
        }
        finally
        {
            isValidating = false;
        }
    }
    else
    {
        errorMessage = "アンケートコードを入力してください。アンケートに参加するにはコードが必要です。";
    }
}
```

### 4. UIの改善：検証中の状態表示

Questionair.razorのボタン部分を修正して、検証中の状態を表示します：

```html
<div class="input-group">
    <input type="text" class="form-control" id="uniqueCode" 
           @bind="inputUniqueCode" placeholder="アンケートコードを入力してください" />
    <button class="btn btn-outline-primary" type="button" @onclick="NavigateToSurvey" disabled="@isValidating">
        @if (isValidating)
        {
            <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
            <span class="ms-2">検証中...</span>
        }
        else
        {
            <span>アンケートに参加</span>
        }
    </button>
</div>
```

### 5. 必要な追加フィールド

```csharp
// Questionair.razorの@codeブロック内に追加
private bool isValidating = false;
```

### 6. RefreshActiveQuestionメソッドの改善

エラーハンドリングを改善して、存在しないコードの場合も適切にエラーメッセージを表示するようにします：

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
        if (activeQuestion != null && activeQuestion.QuestionId != Guid.Empty)
        {
            Console.WriteLine($"Active question received: {activeQuestion.Text}");
            
            // クライアントIDに基づいて重複チェック
            var currentResponses = activeQuestion.Responses.Where(r => r.ClientId == clientId);
            hasSubmitted = currentResponses.Any();
            
            if (hasSubmitted)
            {
                Console.WriteLine($"User with client ID {clientId} has already submitted a response");
            }
            
            // エラーメッセージをクリア（成功時）
            errorMessage = "";
        }
        else
        {
            Console.WriteLine("No active question at this time");
            activeQuestion = null; // Ensure null if empty result returned
            
            // 既にコードの検証は済んでいるので、アクティブな質問がない場合はエラーではない
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error refreshing active question: {ex.Message}");
        
        // 接続エラーの場合のみエラーメッセージを更新
        if (ex.Message.Contains("Connection") || ex.Message.Contains("connect"))
        {
            errorMessage = $"サーバーとの接続に問題があります: {ex.Message}";
        }
        // 404エラーの場合は存在しないコード
        else if (ex is HttpRequestException httpEx && httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            errorMessage = "入力されたアンケートコードは存在しません。正しいコードを入力してください。";
        }
    }
}
```

## 実装手順

1. **バックエンドAPI拡張**:
   - `QuestionGroupExistsQuery`クラスの追加（存在確認用のクエリ）
   - API Serviceに検証エンドポイントを追加

2. **APIクライアント拡張**:
   - `QuestionApiClient`に`ValidateUniqueCodeAsync`メソッドを追加

3. **Questionair.razorの修正**:
   - `isValidating`フィールドの追加
   - `NavigateToSurvey`メソッドの修正
   - 検証中のUI表示の改善
   - `RefreshActiveQuestion`メソッドのエラーハンドリング改善

4. **テスト**:
   - 存在しないコードを入力した場合にエラーメッセージが表示されることを確認
   - 有効なコードを入力した場合は正常に動作することを確認
   - ネットワークエラー時の動作確認

## 技術的考慮事項

1. **ユーザー体験**:
   - コード検証中はボタンを無効化し、スピナーを表示
   - エラーメッセージは明確で具体的に
   - 検証が完了するまでナビゲーションを行わない

2. **エラーハンドリング**:
   - ネットワークエラーと存在しないコードエラーを区別
   - 例外の種類に応じて適切なメッセージを表示

3. **パフォーマンス**:
   - コード検証は軽量なAPIエンドポイントで実装
   - ユーザー体験を損なわないよう検証は高速に

4. **セキュリティ**:
   - 入力値のサニタイズを確実に行う
   - エラーメッセージには詳細なシステム情報を含めない

## まとめ

この設計によって、存在しないアンケートコードが入力された場合に、ユーザーに明確なエラーメッセージを表示することができます。これにより、ユーザーは正しいコードを入力するよう促され、無限に待つという不満な体験を回避できます。また、検証中の状態をUIに表示することで、ユーザーに何が起きているかを明確に伝えることができます。🎉