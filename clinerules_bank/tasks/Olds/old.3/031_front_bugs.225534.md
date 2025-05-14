# GitHub Copilot

## バグ修正計画：単一回答モードで発生するTypeエラーの修正

### 問題の概要
現在、アンケートで`AllowMultipleResponses = false`の場合（単体質問回答モード）において、以下のエラーが発生しています：

```
System.InvalidCastException: Unable to cast object of type 'System.String' to type 'System.Boolean'.
   at EsCQRSQuestions.Web.Components.Pages.Questionair.<>c__DisplayClass0_1.<BuildRenderTree>b__5(ChangeEventArgs e) in /Users/tomohisa/dev/test/EsStudy0314/EsCQRSQuestions/EsCQRSQuestions.Web/Components/Pages/Questionair.razor:line 111
```

### 原因の特定
問題のあるコードは、Questionair.razorの111行目付近にあります：

```csharp
<input class="form-check-input" type="radio" name="questionOptions" 
       id="option-@option.Id" value="@option.Id" 
       checked="@(selectedOptionIds.Contains(option.Id))"
       @onchange="(e) => SelectSingleOption(option.Id, (bool)e.Value)" />
```

この部分でエラーが発生している理由は：
- ラジオボタンの`@onchange`イベントで、`(bool)e.Value`としているが、実際には`e.Value`は`string`型のデータが渡されている
- この型変換の試みが失敗し、`InvalidCastException`が発生している

### 修正計画
以下の方法で問題を解決します：

1. `SelectSingleOption`メソッドの引数の型を修正し、`string`型を適切に処理するようにします。

```csharp
private void SelectSingleOption(string optionId, object value)
{
    // ラジオボタンの場合、valueは文字列型で渡されるため、明示的な変換は避ける
    // valueの値がどうであれ、選択されたoptionIdを単一選択としてセットする
    selectedOptionIds.Clear();
    selectedOptionIds.Add(optionId);
}
```

2. もしくは、呼び出し側で適切な型変換を行う：

```csharp
@onchange="(e) => SelectSingleOption(option.Id, e.Value?.ToString() == option.Id.ToString())" 
```

### 実装計画
1. `/Users/tomohisa/dev/test/EsStudy0314/EsCQRSQuestions/EsCQRSQuestions.Web/Components/Pages/Questionair.razor`ファイルを開く
2. 111行目付近のラジオボタンの`@onchange`イベントハンドラを修正
3. `SelectSingleOption`メソッドの引数と実装を適切に修正
4. テストを実行して、`AllowMultipleResponses = false`の場合に正常に動作することを確認

### 確認事項
- 単一回答モードの選択がきちんと動作すること
- 複数回答モードにも影響を与えないこと
- 回答の送信が正しく処理されること

この修正により、タイプキャストエラーを解決し、単一回答モードでも適切に機能するように改善します。