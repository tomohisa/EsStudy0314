LLMモデル名: Gemini

# QuestionDisplayStopped イベント配信の計画

## 1. 目的

`QuestionDisplayStarted` イベントと同様に、`QuestionDisplayStopped` イベントもリアルタイムでクライアントに配信し、管理者と参加者に問題の表示が停止したことを通知できるようにします。

## 2. 影響範囲

- `EsCQRSQuestions.ApiService/OrleansStreamBackgroundService.cs`: `QuestionDisplayStopped` イベントを処理し、SignalRハブ経由で通知を送信するロジックを追加します。
- `EsCQRSQuestions.AdminWeb`: 管理者向け画面で、問題の表示が停止したことをリアルタイムに反映できるようにします。（今回のタスクでは実装しません）
- `EsCQRSQuestions.Web`: 参加者向け画面で、問題の表示が停止したことをリアルタイムに反映できるようにします。（今回のタスクでは実装しません）

## 3. 実装方針

`OrleansStreamBackgroundService.cs` の `OnNextAsync` メソッド内に、`QuestionDisplayStopped` イベントのケースを追加します。

```csharp
// ... existing code ...
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

            // ここから追加
            case QuestionDisplayStopped displayStopped:
                // QuestionエンティティをロードしてQuestionGroupIdを取得
                var questionResult = await _sekibanOrleansExecutor.LoadAggregateAsync<QuestionProjector>(item.PartitionKeys);
                if (questionResult.IsFailure)
                {
                    // エラーハンドリング: Questionのロードに失敗した場合
                    Console.WriteLine($"Error loading question for QuestionDisplayStopped: {item.PartitionKeys.AggregateId}");
                    break;
                }
                var questionPayload = questionResult.GetValue().ToTypedPayload<Question>();
                if (questionPayload == null)
                {
                    Console.WriteLine($"Error: Question payload is null for QuestionDisplayStopped: {item.PartitionKeys.AggregateId}");
                    break;
                }

                // QuestionGroupエンティティをロードしてUniqueCodeを取得
                var questionGroupResult = await _sekibanOrleansExecutor.LoadAggregateAsync<QuestionGroupProjector>(PartitionKeys.Existing<QuestionGroupProjector>(questionPayload.QuestionGroupId));
                if (questionGroupResult.IsFailure)
                {
                    // エラーハンドリング: QuestionGroupのロードに失敗した場合
                    Console.WriteLine($"Error loading question group for QuestionDisplayStopped: {questionPayload.QuestionGroupId}");
                    break;
                }
                var groupPayload = questionGroupResult.GetValue().ToTypedPayload<QuestionGroup>();
                 if (groupPayload == null)
                {
                    Console.WriteLine($"Error: QuestionGroup payload is null for QuestionDisplayStopped: {questionPayload.QuestionGroupId}");
                    break;
                }

                // 特定のグループに通知を送信
                await _hubService.NotifyUniqueCodeGroupAsync(groupPayload.UniqueCode, "QuestionDisplayStopped",
                    new { QuestionId = item.PartitionKeys.AggregateId });
                break;
            // ここまで追加

            default:
// ... existing code ...
```

## 4. 考慮事項

- **エラーハンドリング**: `Question` または `QuestionGroup` の読み込みに失敗した場合の適切なエラーハンドリングを実装する必要があります。現状はコンソールに出力するのみですが、将来的にはより堅牢なエラー処理を検討します。
- **通知先のグループ**: `QuestionDisplayStarted` と同様に、`Question` に紐づく `QuestionGroup` の `UniqueCode` を使用して、正しいグループにのみ通知を送信します。これにより、他のグループに誤って通知が送信されることを防ぎます。
- **テスト**: ユニットテストおよび結合テストを作成し、イベント配信が正しく行われることを確認します。（今回のタスクでは実装しません）

## 5. 調査が必要な点

- 現状の `OrleansStreamBackgroundService.cs` のエラーハンドリング方針について確認が必要です。エラーが発生した場合、ログ出力のみで良いのか、あるいは特定のエラー通知を行うべきかなど。
- `QuestionDisplayStopped` イベントが発生する具体的なシナリオと、その際にクライアント側でどのような表示更新が期待されるかを確認する必要があります。（今回のタスクでは実装しません）
