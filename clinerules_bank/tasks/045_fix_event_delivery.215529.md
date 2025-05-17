# LLM Model: [ここにあなたのLLMモデル名を記入してください]

## タスク概要

`QuestionDisplayStopped` イベントが発生した際に、関連するクライアント（特定のユニークコードを持つグループ）に通知を送信する機能を実装します。
既存の `QuestionDisplayStarted` イベントの処理を参考に、同様のロジックで `QuestionDisplayStopped` イベントを処理します。

## 調査事項

1.  **`QuestionDisplayStarted` イベント処理の確認:** (完了)
    *   `EsCQRSQuestions/EsCQRSQuestions.ApiService/OrleansStreamBackgroundService.cs` の `OnNextAsync` メソッド内の `QuestionDisplayStarted` ケースの処理内容を確認しました。
    *   `Question` アグリゲートと `QuestionGroup` アグリゲートをロードし、`QuestionGroup` の `UniqueCode` を使用して特定のクライアントグループに通知を送信しています。

2.  **`QuestionDisplayStopped` イベント定義の確認:** (完了)
    *   `EsCQRSQuestions/EsCQRSQuestions.Domain/Aggregates/Questions/Events/QuestionDisplayStopped.cs` を確認しました。
    *   現状、イベントペイロードに追加のプロパティはありません。通知に必要な情報（例: `QuestionId`）は、イベントのメタデータや関連アグリゲートから取得する必要があります。

3.  **通知に必要な情報の特定:**
    *   `QuestionDisplayStarted` と同様に、`QuestionId` を通知に含めるのが適切と考えられます。
    *   `QuestionDisplayStopped` イベントの `PartitionKeys.AggregateId` が `QuestionId` に相当します。

## 実装計画

`EsCQRSQuestions/EsCQRSQuestions.ApiService/OrleansStreamBackgroundService.cs` の `OnNextAsync` メソッドに、`QuestionDisplayStopped` イベントを処理するケースを追加します。

```csharp
// OrleansStreamBackgroundService.cs

// ... 既存の using ディレクティブ ...
// using EsCQRSQuestions.Domain.Aggregates.Questions.Events; // 既に追加されているはず

// ... OnNextAsync メソッド内 ...
switch (item.GetPayload())
{
    // ... 既存のケース ...

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

    // ▼▼▼ 新しく追加するケース ▼▼▼
    case QuestionDisplayStopped displayStopped: // イベントの型を正しく指定
        // QuestionDisplayStarted と同様のロジックで Question と QuestionGroup をロード
        await _sekibanOrleansExecutor.LoadAggregateAsync<QuestionProjector>(item.PartitionKeys) // item.PartitionKeys は Question のもの
            .Conveyor(aggregate => aggregate.ToTypedPayload<Question>()) // Question ペイロードを取得
            .Combine(questionPayload => // questionPayload は Question
                _sekibanOrleansExecutor
                    .LoadAggregateAsync<QuestionGroupProjector>(PartitionKeys.Existing<QuestionGroupProjector>(questionPayload.Payload.QuestionGroupId)) // QuestionGroupId を使って QuestionGroup をロード
                    .Conveyor(groupAggregate => groupAggregate.ToTypedPayload<QuestionGroup>()) // QuestionGroup ペイロードを取得
            )
            .Do(async (question, group) => // question は Question, group は QuestionGroup
            {
                // 特定のユニークコードを持つグループに通知
                await _hubService.NotifyUniqueCodeGroupAsync(
                    group.Payload.UniqueCode, // QuestionGroup の UniqueCode
                    "QuestionDisplayStopped", // 通知するイベント名
                    new { QuestionId = question.PartitionKeys.AggregateId } // 通知するデータ (QuestionId)
                );
            });
        break;
    // ▲▲▲ ここまで追加 ▲▲▲

    default:
        // For other event types, just log the event type
        Console.WriteLine($"Received event: {eventType} for aggregate {aggregateId}");
        break;
}
```

## 確認事項

*   `QuestionDisplayStopped` イベントが発生した際に、`Question` アグリゲートから `QuestionGroupId` を正しく取得できるか。
    *   `QuestionDisplayStarted` の実装を見る限り、`Question` ペイロード内に `QuestionGroupId` が含まれているため、同様に取得可能と判断します。
*   通知先のクライアントグループを特定するための `UniqueCode` は `QuestionGroup` アグリゲートに含まれているか。
    *   `QuestionDisplayStarted` の実装で `group.Payload.UniqueCode` を使用しているため、同様に取得可能と判断します。
*   通知するメッセージの形式（`new { QuestionId = ... }`）は適切か。
    *   `QuestionDisplayStarted` と一貫性を持たせるため、同じ形式とします。

## その他

*   エラーハンドリングやロギングは、既存の処理に倣って適切に追加することを検討します（この計画では省略）。
*   テストコードの作成も必要ですが、このタスクの範囲外とします。
