clinerules_bank/tasks/018_uniquecode.Changes.md
clinerules_bank/tasks/018_uniquecode.md
clinerules_bank/tasks/019_uniquecode_notify.234018.md
clinerules_bank/tasks/019_uniquecode_notify.md
でUniqueCodeだけで表示される動作を実装中なのですが、

EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor
で表示したコンポーネントから
グループを作る
グループの中に質問を作る
質問に対して{Start Display}ボタンを押す

を行ったのですが、クライアント側に質問が表示されませんでした。

その修正のために、特に、ドメインと

EsCQRSQuestions/EsCQRSQuestions.ApiService/IHubNotificationService.cs
EsCQRSQuestions/EsCQRSQuestions.ApiService/QuestionHub.cs
EsCQRSQuestions/EsCQRSQuestions.ApiService/HubNotificationService.cs

EsCQRSQuestions/EsCQRSQuestions.Web/Components/Pages/Questionair.razor
EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor
を精査して問題を探してください。

わからなければ関連項目も探して良いです。


ただ、このタスクでは計画するだけです。
このファイルの下部に計画をよく考えて追記で記入してください。必要なファイルの読み込みなど調査を行い、できるだけ具体的に計画してください。

チャットではなく、以下の新しいファイル

clinerules_bank/tasks/020_start_display_with_right_code.[hhmmss 編集を開始した時間、分、秒].md

に現在の設計を書いてください。

