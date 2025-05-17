# 集計バグ修正計画 - GitHub Copilot

## 問題概要

EsCQRSQuestions/EsCQRSQuestions.Web/Components/Pages/Questionair.razorファイルで、質問の統計情報を表示する際に♾️%やNaN%などの不正なパーセンテージが表示される問題があります。

現在の実装では、回答のパーセンテージ計算が不適切で、1人が複数の回答を選択した場合でも適切に集計されていません。

問題箇所は以下のコードです：

```csharp
var percentage = activeQuestion.Responses.Any() 
    ? (count * 100.0 / activeQuestion.Responses.Count(r => r.ClientId == r.ClientId.Split('-')[0])) 
    : 0;
```

この計算では、分母の値が誤っており、`r.ClientId == r.ClientId.Split('-')[0]`という条件は常にtrueになるため、単に`activeQuestion.Responses.Count()`と同じになるはずですが、何らかの理由で不正な結果を生んでいます。

## 解決アプローチ

問題は、各選択肢の回答割合を計算する際の分母（母数）が正しくないことです。仕様によれば、分母は「回答者数（ユニークなクライアントID数）」であるべきです。

修正方法は以下の通りです：

1. 回答者数（ユニーククライアントID数）を正しく計算する方法に変更
2. パーセンテージ計算の安全性を確保（ゼロ除算対策）

## 実装計画

### 1. Questionair.razor の修正

問題のあるパーセンテージ計算部分を修正します：

```csharp
@foreach (var option in activeQuestion.Options)
{
    // 現在のオプションを選択した回答数
    var count = activeQuestion.Responses.Count(r => r.SelectedOptionId == option.Id);
    
    // ユニークな回答者数を取得（クライアントIDでグループ化）
    var uniqueRespondents = activeQuestion.Responses
        .Select(r => r.ClientId)
        .Distinct()
        .Count();
    
    // 安全なパーセンテージ計算（ゼロ除算対策）
    var percentage = uniqueRespondents > 0 
        ? (count * 100.0 / uniqueRespondents)
        : 0;
    
    <div class="col-md-6 mb-3">
        <div>@option.Text</div>
        <div class="progress">
            <div class="progress-bar" role="progressbar" style="width: @percentage%;" 
                 aria-valuenow="@percentage" aria-valuemin="0" aria-valuemax="100">
                @count (@percentage.ToString("0.0")%)
            </div>
        </div>
    </div>
}
```

この修正により、以下の効果が期待できます：

1. クライアントIDを`Distinct()`で重複除去し、実際の回答者数を正確に計算
2. 回答者数が0の場合は0%と表示（ゼロ除算回避）
3. 1人が複数回答した場合、それぞれの選択肢について正しい割合が表示される

### 2. テスト計画

以下のケースをテストして、正しく修正されたことを確認：

1. 単一回答の場合
   - 1人が1つの選択肢を回答 → その選択肢は100%、他は0%
   - 複数人が異なる選択肢を回答 → それぞれの割合が適切に計算される

2. 複数回答の場合
   - 1人が複数の選択肢を回答 → 選択したすべての選択肢が100%
   - 複数人が複数の選択肢を回答 → それぞれの割合が適切に計算される

### 3. 注意事項

- `r.ClientId == r.ClientId.Split('-')[0]`という条件は常に真になるため、単に`Responses.Count()`と同じになるはず
- これが現在の問題を引き起こしている可能性があります（クライアントIDのフォーマットに関する誤解？）
- 修正案では単純に`Distinct()`を使って一意のクライアントIDを計算し、より明確かつ確実にします

## 実装手順

1. EsCQRSQuestions.Web/Components/Pages/Questionair.razorファイルを開く
2. 回答統計を表示する部分のコードを特定（約177行目あたり）
3. パーセンテージ計算のコードを上記の修正案で置き換える
4. アプリケーションをビルドして実行
5. 実際に質問に回答してパーセンテージ表示が正しいことを確認

## まとめ

この修正により、回答者数を正しく計算し、ユーザーに正確な統計情報を提供できるようになります。また、♾️%やNaN%などの表示問題も解消されます。