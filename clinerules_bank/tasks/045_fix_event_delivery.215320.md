# モデル: Cline

# QuestionDisplayStopped イベント配信の実装計画

## 現状分析

現在、`QuestionDisplayStarted` イベントは `OrleansStreamBackgroundService.cs` で配信されていますが、`QuestionDisplayStopped` イベントはまだ配信されていません。複数のグループがあるため、間違ったグループに送信しないように実装する必要があります。

### 既存実装の確認ポイント

1. `QuestionDisplayStarted` イベントの処理:
   ```csharp
   case QuestionDisplayStarted displayStarted: 
       await _sekibanOrleansExecutor.LoadAggregateAsync<QuestionProjector>(item.PartitionKeys)
           .Conveyor(aggregate => aggregate.ToTypedPayload<Question>())
           .Combine(aggregate =>
               _sekibanOrleansExecutor
                   .LoadAggregateAsync<QuestionGroupProjector>(PartitionKeys.Existing<QuestionGroupProjector>(aggregate.Payload.QuestionGroupId)).Conveyor(group => group.ToTypedPayload<QuestionGroup>()))
           .Do(async (question, group) =>
           {
               await _hubService.NotifyUniqueCodeGroupAsync(group.Payload.UniqueCode, "QuestionDisplayStarted",
                   new { QuestionId = question.PartitionKeys.AggregateId });
           });
       break;
   ```

2. 処理の流れ:
   - 質問アグリゲートを読み込む
   - 質問からグループIDを取得
   - グループアグリゲートを読み込む
   - グループのUniqueCodeを使用して正しいグループだけに通知する

3. イベント構造:
   - `QuestionDisplayStarted`: パラメータなしのイベント
   - `QuestionDisplayStopped`: 同様にパラメータなしのイベント

## 実装計画

### 1. `OrleansStreamBackgroundService.cs` の修正

`OnNextAsync` メソッド内のswitch文に、`QuestionDisplayStopped` の処理を追加します:

```csharp
case QuestionDisplayStopped displayStopped:
    await _sekibanOrleansExecutor.LoadAggregateAsync<QuestionProjector>(item.PartitionKeys)
        .Conveyor(aggregate => aggregate.ToTypedPayload<Question>())
        .Combine(aggregate =>
            _sekibanOrleansExecutor
                .LoadAggregateAsync<QuestionGroupProjector>(
                    PartitionKeys.Existing<QuestionGroupProjector>(aggregate.Payload.QuestionGroupId))
                .Conveyor(group => group.ToTypedPayload<QuestionGroup>()))
        .Do(async (question, group) =>
        {
            await _hubService.NotifyUniqueCodeGroupAsync(group.Payload.UniqueCode, "QuestionDisplayStopped",
                new { QuestionId = question.PartitionKeys.AggregateId });
        });
    break;
```

### 2. 処理の説明

1. `QuestionDisplayStopped` イベントを受け取ったとき:
   - 該当の質問アグリゲート(`QuestionProjector`)を読み込む
   - `ToTypedPayload<Question>()` で型付きのペイロードに変換
   - 質問のプロパティから `QuestionGroupId` を取得
   - 該当のグループアグリゲート(`QuestionGroupProjector`)を読み込む
   - グループの `UniqueCode` を使用して、そのグループだけに通知

2. 通知内容:
   - イベント名: "QuestionDisplayStopped"
   - データ: 質問ID (`{ QuestionId = question.PartitionKeys.AggregateId }`)

### 3. 安全性の確保

- 質問が特定のグループに属していることを確認するため、質問オブジェクトから得られた `QuestionGroupId` を使用
- `NotifyUniqueCodeGroupAsync` を使用して、特定のグループコードを持つクライアントにのみ通知

### 4. テスト計画

1. 質問を表示停止するコマンドを実行
2. イベントが正しく配信されるか確認
3. 関連するグループのクライアントのみが通知を受け取るか確認
4. 他のグループのクライアントは通知を受け取らないことを確認

## 注意点

- クライアント側では、"QuestionDisplayStopped" イベントを処理するためのリスナーを実装する必要があります
- 処理中にエラーが発生した場合は、適切にエラーハンドリングを行います
- 既存の `QuestionDisplayStarted` 処理との一貫性を維持することが重要です

## まとめ

`QuestionDisplayStopped` イベントの配信を実装するために、既存の `QuestionDisplayStarted` 処理と同様のパターンを使用します。質問の所属グループを確認し、そのグループの一意のコードを使用して、正しいグループにのみ通知を送信します。これにより、異なるグループ間でイベントが混在することを防ぎます。😊
