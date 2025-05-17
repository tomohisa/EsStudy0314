# 修正案・問題特定（2025-04-17 00:04:53）

## 問題の本質
- Planning.razorからStartDisplayQuestionでuniqueCodeを渡しているが、クライアント(Questionair.razor)で質問が表示されない。
- SignalRのグループ通知「QuestionDisplayStarted」がuniqueCodeグループに正しく届いていない、もしくはクライアントがグループにJoinできていない可能性が高い。

## 詳細調査結果

### 1. Planning.razor/QuestionHubService
- StartDisplayQuestionForGroupはuniqueCodeが空でなければ呼ばれている。
- uniqueCodeの値はグループ選択時にgroups.FirstOrDefault(g => g.Id == selectedGroupId)?.UniqueCodeで取得している。
- uniqueCodeがnullや空文字の場合は通知されない仕様。

### 2. QuestionHub.cs
- JoinAsSurveyParticipant(string uniqueCode)でGroups.AddToGroupAsync(Context.ConnectionId, uniqueCode)を呼んでいる。
- ただし、Questionair.razorからJoinAsSurveyParticipantが呼ばれるのはUniqueCodeがURLパラメータで渡っている場合のみ。
- uniqueCodeが空の場合、グループにJoinしない。

### 3. HubNotificationService.cs
- NotifyUniqueCodeGroupAsyncはuniqueCodeが空でなければグループにSendAsyncしている。

### 4. Questionair.razor
- SignalR接続時にUniqueCodeがURLパラメータで渡っていればJoinAsSurveyParticipant(uniqueCode)を呼ぶ。
- On("QuestionDisplayStarted", ...)でactiveQuestionを再取得している。

### 5. 問題の再現パターン
- uniqueCodeが空文字やnullの場合、グループJoinも通知も行われないため、クライアントにイベントが届かない。
- グループ作成時や選択時にUniqueCodeが正しく設定されているか要確認。

## 修正案

### A. Planning.razor側
- StartDisplayQuestionでuniqueCodeがnull/空でないことを必ず保証する。
- グループ作成時にUniqueCodeが必ず発行されることを保証する。

### B. Questionair.razor側
- UniqueCodeがURLパラメータで渡っていない場合、グループJoinもイベント受信もできないため、必ずUniqueCode付きでアクセスさせる。
- inputUniqueCodeが空の場合はアンケート画面に遷移させない。

### C. QuestionHub.cs
- JoinAsSurveyParticipantでuniqueCodeが空の場合は警告ログを出す。
- StartDisplayQuestionForGroupでuniqueCodeが空の場合はエラー/警告ログを出す。

### D. ログ強化
- 各所でuniqueCodeの値をConsole.WriteLine/Loggerで出力し、値の流れを可視化する。

### E. ドメイン/API
- QuestionApi.GetActiveQuestionAsync()が正しくactiveQuestionを返しているか確認。
- 必要に応じてAPIの返却値もログ出力。

---

## まとめ・修正手順

1. Planning.razorでuniqueCodeが必ずセットされているかチェックし、空の場合はStartDisplayQuestionを呼ばないようにする。
2. Questionair.razorでUniqueCodeが必須である旨をUIで明示し、空の場合はアンケート画面に遷移させない。
3. QuestionHub.cs/HubNotificationService.csでuniqueCodeが空の場合は警告ログを出す。
4. 各所でuniqueCodeの値をログ出力し、デバッグしやすくする。
5. 必要に応じてAPI/DB側も調査し、activeQuestionの返却値を確認。
