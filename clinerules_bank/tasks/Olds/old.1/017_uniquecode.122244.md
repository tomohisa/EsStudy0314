clinerules_bank/tasks/017_uniquecode.md

# ユニークコード機能の実装計画

## 概要

ユニークコードをURLから取得し、アンケートページで利用できるようにする機能を実装します。

- `https://localhost:7201/questionair/{uniqueCode}` の形式でアクセスできるようにする
- ユニークコードを取得して画面に表示する
- ユニークコードなしで `/questionair/` にアクセスした場合は、ユニークコードを入力できるフォームを表示する

## 必要な変更

### 1. ルーティングの変更

現在の Questionair.razor は `@page "/questionair"` と定義されていますが、これをユニークコードを受け取れるように変更する必要があります。

```csharp
@page "/questionair"
@page "/questionair/{UniqueCode}"
```

### 2. コンポーネント内のパラメータ追加

Questionair.razor コンポーネントにパラメータを追加し、URLからユニークコードを受け取れるようにします。

```csharp
@code {
    [Parameter]
    public string? UniqueCode { get; set; }
    
    // 他の既存のプロパティ
    private HubConnection hubConnection;
    // ...
}
```

### 3. UI の変更

ユニークコードの入力フォームと表示部分を実装します。

1. ユニークコードがある場合:
   - 画面上にユニークコードを表示する
   - SignalR 接続時にユニークコードを送信する

2. ユニークコードがない場合:
   - ユニークコード入力フォームを表示する
   - 入力後、そのコードでリダイレクトする

### 4. 実装計画

1. **Questionair.razor のページディレクティブ変更**
   - 複数のルートを受け付けるように変更

2. **パラメータ追加とその処理**
   - UniqueCode パラメータを追加
   - OnInitialized で UniqueCode の有無を確認

3. **UI コンポーネントの実装**
   - カードの上部にユニークコードを表示するセクションを追加
   - ユニークコードがない場合の入力フォームを実装

4. **入力フォームのロジック実装**
   - 入力されたユニークコードを検証
   - 有効なコードの場合、そのコードのURLにリダイレクト

5. **SignalR 連携**
   - ユニークコードをハブに送信
   - ハブ接続時にユニークコードをパラメータとして渡す

## 具体的な実装

### 1. Questionair.razor 変更

```csharp
@page "/questionair"
@page "/questionair/{UniqueCode}"
```

### 2. パラメータとコードの追加

```csharp
[Parameter]
public string? UniqueCode { get; set; }

private string inputUniqueCode = "";
```

### 3. UI実装 - ユニークコード表示セクション

カードヘッダーの下にユニークコード表示セクションを追加:

```html
<div class="card-body">
    @if (!string.IsNullOrEmpty(UniqueCode))
    {
        <div class="alert alert-info mb-3">
            <small>Survey Code: <strong>@UniqueCode</strong></small>
        </div>
    }
    else
    {
        <div class="mb-3">
            <label for="uniqueCode" class="form-label">Enter Survey Code</label>
            <div class="input-group">
                <input type="text" class="form-control" id="uniqueCode" 
                       @bind="inputUniqueCode" placeholder="Enter survey code" />
                <button class="btn btn-outline-primary" type="button" @onclick="NavigateToSurvey">
                    Go to Survey
                </button>
            </div>
        </div>
    }
    
    <!-- 既存のエラーメッセージ表示 -->
    @if (!string.IsNullOrEmpty(errorMessage))
    {
        <div class="alert alert-danger" role="alert">
            @errorMessage
        </div>
    }
    
    <!-- 残りの既存UI -->
</div>
```

### 4. ナビゲーションロジック

```csharp
private void NavigateToSurvey()
{
    if (!string.IsNullOrWhiteSpace(inputUniqueCode))
    {
        // URLに含まれない文字を削除/置換
        var sanitizedCode = Uri.EscapeDataString(inputUniqueCode.Trim());
        NavigationManager.NavigateTo($"/questionair/{sanitizedCode}");
    }
    else
    {
        errorMessage = "Please enter a valid survey code";
    }
}
```

### 5. SignalR接続への統合

OnInitializedAsync メソッドの中で、SignalR接続を開始する前にユニークコードを渡す:

```csharp
protected override async Task OnInitializedAsync()
{
    // Set up SignalR connection
    hubConnection = new HubConnectionBuilder()
        .WithUrlWithClientFactory("https+http://apiservice/questionHub", HttpMessageHandlerFactory)
        // 既存のコード...
        .Build();
    
    // 他の既存コード...
    
    // Join as survey participant - ユニークコードを渡す
    await hubConnection.InvokeAsync("JoinAsSurveyParticipant", UniqueCode);
}
```

## サーバー側の処理

この計画ではサーバー側の変更は行いませんが、将来的には QuestionHub.cs を拡張してユニークコードを受け取り処理できるようにする必要があるかもしれません。

## 必要なファイル確認状況

- EsCQRSQuestions/EsCQRSQuestions.Web/Components/Pages/Questionair.razor を確認済み
- EsCQRSQuestions/EsCQRSQuestions.Web/Components/Routes.razor を確認済み

## 次のステップ

1. 上記の実装を行う
2. SignalRのサーバー側（QuestionHub）がユニークコードをどう処理するかを検討
3. 将来的にはユニークコードと特定の質問グループなどを関連付ける機能を追加
