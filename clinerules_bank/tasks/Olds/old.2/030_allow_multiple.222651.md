# GitHub Copilot

## 複数回答機能の実装計画

質問に対して複数の回答を許可するか単一の回答のみを許可するかを設定できるようにする機能の実装計画を以下に示します。

### 1. ドメインモデルの変更

#### 1.1 Question クラスの変更

まず、`Question` レコードに複数回答を許可するかどうかのフラグを追加します。

```csharp
[GenerateSerializer]
public record Question(
    string Text,
    List<QuestionOption> Options,
    bool IsDisplayed,
    List<QuestionResponse> Responses,
    Guid QuestionGroupId,
    bool AllowMultipleResponses // 追加：複数回答を許可するかどうか
) : IAggregatePayload;
```

同様に、`DeletedQuestion` レコードも更新する必要があります：

```csharp
[GenerateSerializer]
public record DeletedQuestion(
    string Text,
    List<QuestionOption> Options,
    bool IsDisplayed,
    List<QuestionResponse> Responses,
    Guid QuestionGroupId,
    bool AllowMultipleResponses // 追加：複数回答を許可するかどうか
) : IAggregatePayload;
```

### 2. コマンドの変更

#### 2.1 CreateQuestionCommand の変更

質問作成時に複数回答フラグを設定できるようにします。

```csharp
[GenerateSerializer]
public record CreateQuestionCommand(
    string Text,
    List<QuestionOption> Options,
    Guid QuestionGroupId,
    bool AllowMultipleResponses // 追加：複数回答を許可するかどうか
) : ICommandWithHandler<CreateQuestionCommand, QuestionProjector>
```

#### 2.2 UpdateQuestionCommand の変更

質問更新時にも複数回答フラグを更新できるようにします。

```csharp
[GenerateSerializer]
public record UpdateQuestionCommand(
    Guid QuestionId,
    string Text,
    List<QuestionOption> Options,
    bool AllowMultipleResponses // 追加：複数回答を許可するかどうか
) : ICommandWithHandler<UpdateQuestionCommand, QuestionProjector, Question>
```

#### 2.3 AddResponseCommand の修正

`AddResponseCommand`のハンドラーを修正して、複数回答のバリデーションを実装します。

```csharp
public ResultBox<EventOrNone> Handle(AddResponseCommand command, ICommandContext<Question> context)
{
    // Get the current state of the question
    var question = context.GetAggregate().GetValue().Payload;
    
    // Cannot add a response to a question that is not being displayed
    if (!question.IsDisplayed)
    {
        return new InvalidOperationException("Cannot add a response to a question that is not being displayed");
    }
    
    // Validate the selected option ID
    if (string.IsNullOrWhiteSpace(command.SelectedOptionId))
    {
        return new ArgumentException("Selected option ID cannot be empty");
    }
    
    // Check if the selected option ID exists
    if (!question.Options.Any(o => o.Id == command.SelectedOptionId))
    {
        return new ArgumentException($"Option with ID '{command.SelectedOptionId}' does not exist");
    }
    
    // 複数回答が許可されていない場合、同じClientIdからの回答がすでにあるかチェック
    if (!question.AllowMultipleResponses && 
        question.Responses.Any(r => r.ClientId == command.ClientId))
    {
        return new InvalidOperationException("Multiple responses are not allowed for this question");
    }
    
    // Create the event
    return EventOrNone.Event(new ResponseAdded(
        Guid.NewGuid(),
        command.ParticipantName,
        command.SelectedOptionId,
        command.Comment,
        DateTime.UtcNow,
        command.ClientId));
}
```

### 3. イベントの変更

#### 3.1 QuestionCreated イベントの変更

```csharp
[GenerateSerializer]
public record QuestionCreated(
    string Text,
    List<QuestionOption> Options,
    Guid QuestionGroupId,
    bool AllowMultipleResponses // 追加：複数回答を許可するかどうか
) : IEventPayload;
```

#### 3.2 QuestionUpdated イベントの変更

```csharp
[GenerateSerializer]
public record QuestionUpdated(
    string Text,
    List<QuestionOption> Options,
    bool AllowMultipleResponses // 追加：複数回答を許可するかどうか
) : IEventPayload;
```

### 4. プロジェクターの変更

`QuestionProjector` クラスを更新して、新しいフィールドを処理できるようにします。

```csharp
public IAggregatePayload Project(IAggregatePayload payload, IEvent ev)
    => (payload, ev.GetPayload()) switch
    {
        // Create a new question
        (EmptyAggregatePayload, QuestionCreated created) => new Question(
            created.Text,
            created.Options,
            false,
            new List<QuestionResponse>(),
            created.QuestionGroupId,
            created.AllowMultipleResponses), // 更新：複数回答フラグを追加
        
        // Update an existing question
        (Question question, QuestionUpdated updated) => question with
        {
            Text = updated.Text,
            Options = updated.Options,
            AllowMultipleResponses = updated.AllowMultipleResponses // 更新：複数回答フラグを更新
        },
        
        // 他のケースは変更なし
        // ...
        
        // Delete a question
        (Question question, QuestionDeleted _) => new DeletedQuestion(
            question.Text,
            question.Options,
            question.IsDisplayed,
            question.Responses,
            question.QuestionGroupId,
            question.AllowMultipleResponses), // 更新：複数回答フラグを追加
        
        // Default case - return the payload unchanged
        _ => payload
    };
```

### 5. クエリの変更

#### 5.1 ActiveQuestionQuery の変更

`ActiveQuestionRecord` に複数回答フラグを追加します。

```csharp
[GenerateSerializer]
public record ActiveQuestionRecord(
    Guid QuestionId,
    string Text,
    List<QuestionOption> Options,
    List<ResponseRecord> Responses,
    Guid QuestionGroupId,
    bool AllowMultipleResponses // 追加：複数回答を許可するかどうか
);
```

#### 5.2 QuestionListQuery の変更

`QuestionSummaryRecord` にも複数回答フラグを追加します。

```csharp
[GenerateSerializer]
public record QuestionSummaryRecord(
    Guid QuestionId,
    string Text,
    int OptionCount,
    bool IsDisplayed,
    int ResponseCount,
    int Order = 0, // 表示順序（デフォルト値は0）
    bool AllowMultipleResponses = false // 追加：複数回答を許可するかどうか
);
```

### 6. 実装上の注意点

1. デフォルト値として、既存の質問は単一回答（`AllowMultipleResponses = false`）とする。
2. マイグレーション対策として、新しいフィールドがない古いイベントを処理する場合のフォールバック処理を検討する。
3. 既存の回答が追加されたときのビジネスルールを明確にする（単一回答の質問に既に回答済みの場合、古い回答を削除するか、拒否するか）。

### 7. 移行手順

1. ドメインモデル（Question, DeletedQuestion）に新しいフィールドを追加
2. コマンド（CreateQuestionCommand, UpdateQuestionCommand）を更新
3. イベント（QuestionCreated, QuestionUpdated）を更新
4. QuestionProjector を修正して新しいフィールドを扱えるようにする
5. AddResponseCommand のハンドラーを修正してバリデーションロジックを追加
6. クエリ（ActiveQuestionQuery, QuestionListQuery）の戻り値を更新
7. 既存データの移行戦略を検討（必要に応じてマイグレーションイベントを作成）

これらの変更は、ドメインレベルでの実装に必要な変更です。API層とUI層の変更は、このタスクの範囲外とのことですので、別のタスクで対応することになります。ただし、将来の参考のために、API層とUI層で必要な変更についても簡単に概要を示します。

### 将来のタスク：API および UI の修正

1. API エンドポイントの更新
   - 質問作成/更新エンドポイントで複数回答フラグを受け付ける
   - 回答の追加時に適切なバリデーションを行う

2. UI コンポーネントの更新
   - 質問作成/編集フォームに複数回答を許可するチェックボックスを追加
   - 回答表示時に複数選択が可能かどうかを視覚的に示す
   - 複数回答可能な質問では、チェックボックスまたは複数選択可能なUIコンポーネントを使用

以上が複数回答機能の実装計画です。現在のコードベースを基に、Event Sourcing パターンを維持しながら、最小限の変更で機能を追加することを目指しています。