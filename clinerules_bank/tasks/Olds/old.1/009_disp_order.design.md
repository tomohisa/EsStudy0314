# 質問表示順序（Order）の実装設計

## 目的
質問グループ内の質問に表示順序を追加し、UI上でもその順序で表示できるようにする。これにより、ユーザーが希望する順序で質問を提示することが可能になります。

## 現状分析
現在のシステムでは：

1. `QuestionReference.cs`では既に`Order`プロパティ（表示順序）が定義されています：
```csharp
public record QuestionReference(
    Guid QuestionId,
    int Order
);
```

2. `QuestionsMultiProjector.cs`では`QuestionGroupInfo`内で質問リスト（`List<QuestionReference>`）として順序付けされた質問参照を保持しています：
```csharp
public record QuestionGroupInfo(Guid GroupId, string Name, List<QuestionReference> Questions);
```

3. しかし`QuestionInfo`レコードには順序情報がありません。

4. `QuestionsQuery.cs`の`QuestionDetailRecord`にも順序情報が含まれていません。

5. 現在の質問の並べ替えは`QuestionGroupName` → `IsDisplayed` → `Text`の順に行われています。

6. `AllQuestionsList.razor`では質問の順序が表示されておらず、並べ替えも適用されていません。

## 実装計画

### 1. QuestionsMultiProjector.cs の修正

`QuestionInfo`レコードに`Order`プロパティを追加します：

```csharp
[GenerateSerializer]
public record QuestionInfo(
    Guid QuestionId,
    string Text,
    List<QuestionOption> Options,
    bool IsDisplayed,
    List<QuestionResponse> Responses,
    Guid QuestionGroupId,
    string QuestionGroupName, // QuestionGroupの名前
    int Order = 0             // デフォルト値は0
);
```

また、以下のヘルパーメソッドの修正が必要です：

- `AddNewQuestion` - 新しい質問を追加する際に、グループ内の順序情報を取得して設定
- `UpdateQuestionGroupId` - グループ変更時に新しいグループでの順序を設定
- `AddQuestionToGroup` - グループに質問を追加する際の順序情報の伝播
- `UpdateQuestionOrder` - 順序変更時に`QuestionInfo`の`Order`も更新

### 2. QuestionsQuery.cs の修正

`QuestionDetailRecord`に`Order`プロパティを追加します：

```csharp
[GenerateSerializer]
public record QuestionDetailRecord(
    Guid QuestionId,
    string Text,
    List<QuestionOption> Options,
    bool IsDisplayed,
    int ResponseCount,
    Guid QuestionGroupId,
    string QuestionGroupName,
    int Order
);
```

`HandleFilter`メソッド内で`questions`から`QuestionDetailRecord`へのマッピング時に、`Order`プロパティも渡すよう修正します。

`HandleSort`メソッドでの並べ替え順序を次のように変更します：

```csharp
return filteredList
    .OrderBy(q => q.QuestionGroupName)
    .ThenBy(q => q.Order)                 // Order順を優先
    .ThenByDescending(q => q.IsDisplayed) // 次に表示状態
    .ThenBy(q => q.Text)                  // 最後にテキスト
    .AsEnumerable()
    .ToResultBox();
```

### 3. AllQuestionsList.razor の修正

テーブルのヘッダーに「Order」列を追加します：

```html
<thead>
    <tr>
        <th>Order</th>  <!-- 新規追加 -->
        <th>Question</th>
        <th>Group</th>
        <th>Options</th>
        <th>Status</th>
        <th>Responses</th>
        <th>Actions</th>
    </tr>
</thead>
```

テーブルの各行に順序を表示するセルを追加します：

```html
<tr>
    <td>@question.Order</td>  <!-- 新規追加 -->
    <td>@question.Text</td>
    <!-- その他の既存の列 -->
</tr>
```

### 4. QuestionListQuery の更新確認

`QuestionListQuery.QuestionSummaryRecord`にも`Order`プロパティが必要かどうか確認し、必要であれば追加します。

## 将来的な拡張
1. 管理者UIで質問の順序を直接変更できる機能の追加を検討
2. 質問グループ内で質問をドラッグ＆ドロップで並べ替えるUIの実装
3. 参加者向け画面での表示順序の最適化

## 技術的注意点
1. 既存のデータとの互換性 - `Order`プロパティをnullable型にするか、デフォルト値を設定するかを検討
2. 順序変更イベントが発生した際の、マルチプロジェクターでの正確な状態更新の確保
3. 質問グループ間で質問が移動する際の順序の再計算ロジックの検証

## まとめ
この実装により、質問の表示順序を管理し、UIでその順序を反映することができるようになります。これは特に複数の質問を含むアンケートや調査において、論理的な流れを維持するために重要な機能です。
