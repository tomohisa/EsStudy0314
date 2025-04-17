# 019_uniquecode_notify 設計（計画）

## 目的
- AdminWeb（Planning.razor配下）から「表示依頼」等の操作をした際、SignalR経由で該当グループ（UniqueCode）にのみ通知されるようにする。
- 他グループの参加者には関係ないイベントが届かないようにする。

## 調査・必要なファイル
- Planning.razor（および配下のGroupQuestionsList等）で「表示依頼」アクションがどこで発火しているか確認。
- AdminWeb→ApiServiceへの通知経路（QuestionHubServiceなど）を調査。
- ApiService側では既にUniqueCodeグループ通知の仕組みが実装済み（018_uniquecode.Changes.md参照）。

## 具体的な設計手順
1. **UI側（Planning.razor配下）**
   - 「表示依頼」時に、対象グループのUniqueCodeを取得できるようにする（GroupQuestionsList等にUniqueCodeを渡す）。
   - 表示依頼メソッド（例: StartDisplayQuestion）で、UniqueCodeも引数として渡す。

2. **サービス層（QuestionHubService等）**
   - StartDisplayQuestion等のメソッドを拡張し、UniqueCodeを引数で受け取れるようにする。
   - SignalR経由でApiServiceのHubに「UniqueCode付き」でリクエストを送信。

3. **ApiService側**
   - 既存のHubで、UniqueCodeグループにのみ通知するメソッド（NotifyUniqueCodeGroupAsync）が用意されているので、それを利用。

4. **テスト観点**
   - 異なるグループの参加者が他グループの表示依頼イベントを受信しないこと。
   - 管理者は全グループの状態を把握できること。

---

この設計により、UI→サービス→Hubの各層でUniqueCodeを正しく伝播させる修正方針が明確になります。
次の実装タスクでは、各層でUniqueCodeを受け渡すように修正してください。🛠️✨
