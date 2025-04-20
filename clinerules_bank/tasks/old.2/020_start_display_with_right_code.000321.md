# 調査・設計メモ（2025-04-17 00:03:21）

## 調査対象
- EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor
- EsCQRSQuestions/EsCQRSQuestions.Web/Components/Pages/Questionair.razor
- EsCQRSQuestions/EsCQRSQuestions.ApiService/IHubNotificationService.cs
- EsCQRSQuestions/EsCQRSQuestions.ApiService/QuestionHub.cs
- EsCQRSQuestions/EsCQRSQuestions.ApiService/HubNotificationService.cs

## 現状把握
- Planning.razorでグループ作成→質問作成→Start Displayボタン押下までUI操作は正常。
- しかし、クライアント(Questionair.razor)側で質問が表示されない。
- SignalR経由で「QuestionDisplayStarted」イベントがUniqueCodeグループに送信される設計。

## 主要な流れ
1. Planning.razorのStartDisplayQuestionでHubService.StartDisplayQuestionForGroup(questionId, uniqueCode)を呼ぶ。
2. QuestionHub.csのStartDisplayQuestionForGroupでIHubNotificationService.NotifyUniqueCodeGroupAsyncを呼ぶ。
3. HubNotificationService.csで対象グループに「QuestionDisplayStarted」イベントを送信。
4. Questionair.razorでSignalRのOn("QuestionDisplayStarted", ...)で受信し、activeQuestionを再取得して表示。

## 問題の可能性
- uniqueCodeが空文字やnullで送信されていないか
- クライアントが正しいグループ（uniqueCode）にJoinできていない
- サーバ側でグループ名が一致していない
- Questionair.razorのSignalR接続先やイベントハンドラが正しく動作していない
- QuestionApi.GetActiveQuestionAsync()の返却値が正しくない

## 調査・設計計画

1. Planning.razor
   - StartDisplayQuestionでuniqueCodeが正しく渡っているか確認
   - HubService.StartDisplayQuestionForGroupの引数を追跡

2. QuestionHub.cs
   - JoinAsSurveyParticipantでGroups.AddToGroupAsyncがuniqueCodeで呼ばれているか
   - StartDisplayQuestionForGroupでuniqueCodeが空でない場合のみ通知しているか

3. HubNotificationService.cs
   - NotifyUniqueCodeGroupAsyncでグループ名が空でない場合のみSendAsyncしているか

4. Questionair.razor
   - SignalR接続時にJoinAsSurveyParticipant(uniqueCode)が呼ばれているか
   - On("QuestionDisplayStarted", ...)でactiveQuestionの再取得が行われているか

5. ドメイン層
   - QuestionApi.GetActiveQuestionAsync()の返却値が正しいか
   - 必要に応じてAPI/DB側も調査

6. ログ出力
   - 各所でConsole.WriteLine等のログ出力を追加し、値の流れ・イベント発火を確認

7. 必要に応じて以下も調査
   - QuestionApiClient/QuestionGroupApiClientの実装
   - SignalRの接続設定やCORS
   - クライアント側のURLパラメータ（UniqueCode）の受け渡し

---

## 次のアクション
- 上記観点でコード・ログを精査し、どこで値やイベントが途切れているかを特定する
- 必要に応じて追加のファイル読み込みや詳細調査を行う
- 問題箇所が特定できたら修正方針を設計
