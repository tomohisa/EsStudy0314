# Question Group Ordering Implementation Design

## 現在の設計と問題点

現在のシステムでは、AdminWebの`Planning.razor`ページからQuestionを作成する際、以下のフローで処理されています：

1. `Planning.razor`ページで「Create Questions」ボタンがクリックされる
2. `ApiService/Program.cs`内のエンドポイント（235-236行目付近）が呼び出される
3. このエンドポイントは`Question`の作成のみを行い、`QuestionGroup`に関連する`Order`の設定は行われていない

問題点：
- Questionが作成されても、QuestionGroupにOrderが設定されない
- QuestionGroupWorkflowの機能（71-72行目）が活用されていない
- 現状では複数のAPI呼び出しが必要になる可能性がある

## 提案する設計変更

### 1. ApiServiceの拡張

`Program.cs`の該当エンドポイントを拡張し、Question作成後に同じリクエスト内でQuestionGroupのOrderも設定するようにします。

```csharp
app.MapPost("/api/questions", async (CreateQuestionRequest request, CommandDispatcher commandDispatcher) =>
{
    var result = await commandDispatcher.SendAsync(new CreateQuestionCommand(
        // 既存のパラメータ
    ));
    
    // 新規追加：同じトランザクション内でQuestionGroupのOrderも設定
    if (result.IsSuccess)
    {
        await commandDispatcher.SendAsync(new SetQuestionsOrderCommand(
            request.QuestionGroupId,
            // 順序情報
        ));
    }
    
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
});
```

### 2. リクエストDTOの拡張

`CreateQuestionRequest`クラスを拡張して、Order情報も含められるようにします：

```csharp
public class CreateQuestionRequest
{
    // 既存のプロパティ
    public Guid QuestionGroupId { get; set; }
    public string Text { get; set; }
    
    // 新規追加：順序情報
    public List<Guid> QuestionIdsInOrder { get; set; } = new List<Guid>();
}
```

### 3. ワークフローの活用

`QuestionGroupWorkflow.cs`の`SetQuestionsOrder`メソッド（71-72行目付近）を活用します。

### 4. Blazorフロントエンドの拡張

`Planning.razor`ページで、Question作成時にOrderも設定できるよう機能を拡張します。

## 実装ステップ

1. `CreateQuestionRequest` DTOを拡張して順序情報を含める
2. `ApiService/Program.cs`のエンドポイントを拡張し、QuestionGroup内のOrderも設定できるようにする
3. `CommandDispatcher`を使って`SetQuestionsOrderCommand`を送信するロジックを追加
4. フロントエンドの`Planning.razor`で、新しいリクエスト形式に対応するよう修正
5. 必要に応じてエラーハンドリングを追加

## 検討事項

- トランザクションの扱い：Question作成とOrder設定を単一のトランザクションで扱うべきか
- エラー処理：Question作成は成功したがOrder設定で失敗した場合の挙動
- パフォーマンス：複数のコマンドを連続実行する際の最適化
- UI対応：フロントエンドでの順序指定UI実装

この設計変更により、APIの呼び出し回数を減らしつつ、QuestionとQuestionGroupのOrderを適切に設定できるようになります。
