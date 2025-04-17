EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor
でstartするときは、unique code をちゃんと設定しているのに

Stop Displayしているときに、Unique Codeを設定していないので、他のUnique Codeを設定したクライアントもStopDisplayしてしまう。

EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Services/QuestionHubService.cs
や
EsCQRSQuestions/EsCQRSQuestions.ApiService/QuestionHub.cs
EsCQRSQuestions/EsCQRSQuestions.ApiService/HubNotificationService.cs
などもみて、Start と同様にStopも正しく動く様にする方法を考えて

ただ、このタスクでは計画するだけです。
このファイルの下部に計画をよく考えて追記で記入してください。必要なファイルの読み込みなど調査を行い、できるだけ具体的に計画してください。

チャットではなく、以下の新しいファイル

clinerules_bank/tasks/026_bug_stop.[hhmmss 編集を開始した時間、分、秒].md

に現在の設計を書いてください。また、あなたのLLMモデル名もファイルの最初に書き込んでください。
