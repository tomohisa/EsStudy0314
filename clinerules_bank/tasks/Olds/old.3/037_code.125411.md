# モデル: GitHub Copilot

# EsCQRSQuestions.Web/Components/Pages/Questionair.razor の問題分析と解決計画

## 問題の概要

アンケートコードを手入力した後、管理画面からのイベント（スタートディスプレイなど）が画面に反映されないという問題が発生しています。ただし、URLにコードが付いている状態でブラウザをリロードした場合は正しく動作します。これは、コードを入力してナビゲーションする場合と、URLにコードが付いた状態でページをロードする場合で、SignalRの接続とイベントハンドリングの挙動が異なることを示しています。😞

## 現状の動作分析

### 正しく動作するケース（URLにコード付きでリロード）:
1. `OnInitializedAsync()`でSignalR接続が確立される
2. URLのパラメータから`UniqueCode`が取得される
3. `hubConnection.StartAsync()`後に`JoinAsSurveyParticipant`が呼ばれる
4. SignalRのグループに正しく参加して、イベントが受信できる

### 問題が発生するケース（コード手入力後）:
1. コードを入力して「アンケートに参加」ボタンをクリック
2. `NavigateToSurvey()`が呼ばれて`NavigationManager.NavigateTo()`が実行される
3. Blazorの挙動として、ページ全体のリロードではなく、URLとUIのみが更新される
4. **問題点**: この際、SignalRの接続が新しいURLのUniqueCodeでグループに参加していない

## 問題の根本原因

1. コード入力後のナビゲーションはクライアントサイドルーティングであり、`OnInitializedAsync()`は再実行されない
2. そのため、新しいUniqueCodeでSignalRグループに参加するコードが実行されない
3. 結果として、管理画面からのイベント通知が届かない状態になる

## 解決策の計画

### 修正案1: NavigateToSurvey()後にグループに参加する処理を追加

```csharp
private async Task NavigateToSurvey()
{
    if (!string.IsNullOrWhiteSpace(inputUniqueCode))
    {
        // URLに含まれない文字を削除/置換
        var sanitizedCode = Uri.EscapeDataString(inputUniqueCode.Trim());
        
        // UniqueCodeを更新
        UniqueCode = sanitizedCode;
        
        // URLを更新（ただしこれはクライアントサイドルーティング）
        NavigationManager.NavigateTo($"/questionair/{sanitizedCode}");
        
        // 追加: NavigateToSurvey後にSignalRグループに参加
        if (hubConnection.State == HubConnectionState.Connected)
        {
            await hubConnection.InvokeAsync("JoinAsSurveyParticipant", sanitizedCode);
            await RefreshActiveQuestion();
        }
    }
    else
    {
        errorMessage = "アンケートコードを入力してください。アンケートに参加するにはコードが必要です。";
    }
}
```

### 修正案2: OnParametersSetAsync()を利用してUniqueCodeの変更を検知

Blazorのライフサイクルメソッド`OnParametersSetAsync()`はパラメータが変更されるたびに呼ばれるため、これを利用してUniqueCodeの変更を検知し、SignalRグループに参加する処理を追加します。

```csharp
// 前回のUniqueCodeを保存する変数
private string? previousUniqueCode;

protected override async Task OnParametersSetAsync()
{
    // UniqueCodeが変更された場合のみ処理
    if (UniqueCode != previousUniqueCode && !string.IsNullOrEmpty(UniqueCode))
    {
        previousUniqueCode = UniqueCode;
        
        // すでに接続済みの場合は再接続せずにSignalRグループに参加
        if (hubConnection.State == HubConnectionState.Connected)
        {
            await hubConnection.InvokeAsync("JoinAsSurveyParticipant", UniqueCode);
            await RefreshActiveQuestion();
            Console.WriteLine($"Joined survey with unique code: {UniqueCode} in OnParametersSetAsync");
        }
    }
    
    await base.OnParametersSetAsync();
}
```

### 修正案3: 組み合わせアプローチ

両方の修正を組み合わせることで、クライアントサイドルーティングでもURLでの直接アクセスでも、確実にSignalRグループに参加できるようにします。

## 実装手順

1. `previousUniqueCode`フィールドをコンポーネントに追加
2. `OnParametersSetAsync()`メソッドを実装して、UniqueCodeが変更された場合の処理を追加
3. `NavigateToSurvey()`メソッドを修正して、ナビゲーション後にSignalRグループに参加する処理を追加
4. デバッグログを追加して、各ステップでの動作を確認できるようにする

## テスト計画

1. コードを手入力してアンケートに参加した場合に、管理画面からのイベントが画面に反映されるか確認
2. URLにコードが付いた状態でページをリロードした場合も、引き続き正しく動作するか確認
3. コードを手入力→参加→管理画面でディスプレイ開始→別のコードを手入力→参加、という順で操作しても問題なく動作するか確認

## 注意点

1. SignalRの接続状態を確認してから処理を行う
2. 二重接続などによるパフォーマンス問題が発生しないように注意する
3. すでに接続済みのグループに再度参加しようとしても問題ないことを確認する（サーバー側でエラーが発生しないか）