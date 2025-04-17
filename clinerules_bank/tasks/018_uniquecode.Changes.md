# 018_uniquecode 変更設計

## 目的
- SignalRのUniqueCodeグループ機能を活用し、参加者が指定したUniqueCode（アンケートコード）ごとにグループ分離する。
- Adminや他のアンケートの情報が混ざらないよう、該当グループのみに情報を送信する。
- 今回はApiServiceのみ修正対象。他プロジェクトは別タスク。

## 変更内容

### 1. QuestionHub.cs
- `JoinAsSurveyParticipant(string uniqueCode)` を追加済み。呼び出し時に `Groups.AddToGroupAsync(Context.ConnectionId, uniqueCode)` でグループ分離。
- 既存の `JoinAsSurveyParticipant()` も残し、引数なしの場合は従来通り全体グループに参加。

### 2. HubNotificationService.cs
- `NotifyUniqueCodeGroupAsync(string uniqueCode, string method, object data)` を追加済み。
- UniqueCodeグループにのみイベントを送信できるようにする。

### 3. IHubNotificationService.cs
- インターフェースにも `NotifyUniqueCodeGroupAsync` を追加すること。

### 4. OrleansStreamBackgroundService.cs
- QuestionGroupやQuestionのイベント（作成・更新・削除・追加・削除・順序変更など）で、
  - 対象のQuestionGroupのUniqueCodeを取得
  - `NotifyUniqueCodeGroupAsync` を使い、そのグループのみに通知
- 例: 質問追加時は、そのQuestionGroupのUniqueCodeグループにのみ `QuestionAddedToGroup` を送信
- 既存の `NotifyAdminsAsync` などは管理者向けに残す

### 5. QuestionGroupのUniqueCode取得
- OrleansStreamBackgroundServiceでイベント受信時、対象のAggregateIdからUniqueCodeを取得する必要あり。
- 必要に応じて `GetQuestionGroupsQuery` で全グループを取得し、AggregateId→UniqueCodeのマッピングを行う。
- パフォーマンス改善が必要ならキャッシュも検討。

### 6. テスト観点
- 異なるUniqueCodeで接続した複数クライアントが、他のアンケートのイベントを受信しないこと。
- 管理者は全グループのイベントを受信できること。

---

この設計により、ApiServiceのみの修正でUniqueCodeごとのリアルタイム分離配信が可能となる。
今後、AdminWebやWeb側のUI/UX対応は別タスクで実施。
