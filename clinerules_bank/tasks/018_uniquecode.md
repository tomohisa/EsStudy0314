                await hubConnection.InvokeAsync("JoinAsSurveyParticipant", UniqueCode);


EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Services/QuestionHubService.cs
にUniqueCodeを取る機能を追加したい。

UniqueCodeを追加したときに、対象のQuestionGroupだけの情報を送る様にしたい。他のアンケートを購読しているときに関係ないアンケートが表示されない様にしてください。

まずは、ApiServiceの中だけを修正してください。他のプロジェクトの変更はこのチケットでは不要です。

ただ、このタスクでは計画するだけです。
このファイルの下部に計画をよく考えて追記で記入してください。必要なファイルの読み込みなど調査を行い、できるだけ具体的に計画してください。

チャットではなく、以下の新しいファイル

clinerules_bank/tasks/017_uniquecode.[hhmmss 編集を開始した時間、分、秒].md

に現在の設計を書いてください。




