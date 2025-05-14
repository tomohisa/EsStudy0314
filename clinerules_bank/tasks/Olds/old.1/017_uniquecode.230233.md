# 018_uniquecode 設計（計画）

## 目的
- ApiService（EsCQRSQuestions.ApiService）で、UniqueCode（例: JoinAsSurveyParticipantで渡される）を受け取った場合、該当するQuestionGroupの情報だけをクライアントに送信する。
- 他のアンケート（QuestionGroup）の情報が表示されないようにする。
- 今回はApiServiceのみを修正対象とし、他プロジェクトの変更は不要。

## 現状調査
- QuestionHub.cs には JoinAsSurveyParticipant() があり、現状はUniqueCodeを受け取らず、ActiveUsers管理のみ。
- QuestionGroupやUniqueCodeによるグループ分け・情報送信の仕組みは未実装。

## 具体的な設計・作業計画
1. **QuestionHub.csのJoinAsSurveyParticipantを拡張**
    - 引数にUniqueCode(string)を追加。
    - クライアントからUniqueCodeを受け取れるようにする。
2. **SignalRグループの活用**
    - ConnectionIdをUniqueCodeごとのSignalRグループに追加。
    - 例: await Groups.AddToGroupAsync(Context.ConnectionId, UniqueCode);
3. **QuestionGroup情報の送信制御**
    - QuestionGroupの情報送信時、UniqueCodeグループにのみ該当データを送信。
    - 例: await Clients.Group(UniqueCode).SendAsync(...)
4. **QuestionGroup情報の取得方法**
    - UniqueCodeから該当するQuestionGroupを特定するロジックを用意。
    - 必要に応じて、Domain層やExecutor経由で取得。
5. **他のQuestionGroup情報の遮断**
    - 他のUniqueCodeグループには該当しないデータを送信しない。
6. **テスト・動作確認**
    - 複数クライアントで異なるUniqueCodeを指定し、他のアンケートが表示されないことを確認。

## 影響範囲・備考
- 今回はApiServiceのみ。WebやAdminWeb側の変更は別タスク。
- Domain層にUniqueCode→QuestionGroupのマッピングがなければ追加検討。
- SignalRのグループ機能を活用することで、効率的な配信制御が可能。

---

（この設計は 23:02:33 に作成）
