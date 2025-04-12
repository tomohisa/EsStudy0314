EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor
でCreate Questionsしたときに

EsCQRSQuestions/EsCQRSQuestions.ApiService/Program.cs
https://github.com/tomohisa/EsStudy0314/blob/393b3c517a85e6701a30a51f2d89bab20174f396/EsCQRSQuestions/EsCQRSQuestions.ApiService/Program.cs#L235-L236

を呼んでいるのですが、これはQuestionしか作らず、QuestionGroupにOrderを作っていない
EsCQRSQuestions/EsCQRSQuestions.Domain/Workflows/QuestionGroupWorkflow.cs


https://github.com/tomohisa/EsStudy0314/blob/393b3c517a85e6701a30a51f2d89bab20174f396/EsCQRSQuestions/EsCQRSQuestions.Domain/Workflows/QuestionGroupWorkflow.cs#L71-L72

をAPIから呼ぶ様にできる思います。できるだけ、よぶAPIの回数は減らして、APIServiceの中で集約を呼びたい

ただ、このタスクでは計画するだけです。
このファイルの下部に計画をよく考えて追記で記入してください。必要なファイルの読み込みなど調査を行い、できるだけ具体的に計画してください。

チャットではなく、以下の新しいファイル

clinerules_bank/tasks/011_order.design.2.md

に現在の設計を書いてください。


