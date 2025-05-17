# モデル: GitHub Copilot

# QuestionDisplayStopped イベント配信の実装計画

## 1. 現状の把握と分析

現在、`QuestionDisplayStarted` イベントは `OrleansStreamBackgroundService.cs` で以下のように配信されています：

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

この実装では：
1. 質問のアグリゲートを読み込む
2. その質問が属するグループのアグリゲートを読み込む
3. グループの `UniqueCode` を使用して、特定のグループに通知する

## 2. `QuestionDisplayStopped` イベント配信実装計画

`QuestionDisplayStopped` イベントに対しても同様の処理を行う必要があります。以下のコードを `OrleansStreamBackgroundService.cs` の `switch` 文に追加します：

```csharp
case QuestionDisplayStopped displayStopped:
    await _sekibanOrleansExecutor.LoadAggregateAsync<QuestionProjector>(item.PartitionKeys)
        .Conveyor(aggregate => aggregate.ToTypedPayload<Question>())
        .Combine(aggregate =>
            _sekibanOrleansExecutor
                .LoadAggregateAsync<QuestionGroupProjector>(PartitionKeys.Existing<QuestionGroupProjector>(aggregate.Payload.QuestionGroupId)).Conveyor(group => group.ToTypedPayload<QuestionGroup>()))
        .Do(async (question, group) =>
        {
            await _hubService.NotifyUniqueCodeGroupAsync(group.Payload.UniqueCode, "QuestionDisplayStopped",
                new { QuestionId = question.PartitionKeys.AggregateId });
        });
    break;
```

## 3. 実装のポイント

1. **正確なグループ識別**:
   - 質問に紐付けられたグループ ID を使って、正しいグループのみに通知します
   - `QuestionGroupId` を使って `QuestionGroupProjector` からグループ情報を取得します

2. **通知メソッド**:
   - `_hubService.NotifyUniqueCodeGroupAsync` を使用して、特定のグループの参加者に通知します
   - グループの `UniqueCode` を使って、対象のグループを特定します

3. **通知データ**:
   - 質問の ID (`question.PartitionKeys.AggregateId`) のみを送信します
   - フロントエンド側では、この ID を使って表示を更新します

## 4. テスト計画

実装後に以下のテストを行うことをお勧めします：

1. 管理者が質問の表示を停止したとき、正しいグループのみに通知が送られるか
2. 複数のグループがある場合、他のグループに誤って通知が行かないか
3. クライアント側で通知を受け取り、正しく UI が更新されるか

## 5. 注意点

- `QuestionDisplayStopped` イベントは引数を持たない空のイベントなので、必要な情報は全て質問とグループのアグリゲートから取得する必要があります
- メッセージング処理は非同期で行われるため、エラーハンドリングを適切に実装することが重要です
