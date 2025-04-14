EsCQRSQuestions/EsCQRSQuestions.Domain/Aggregates/QuestionGroups/Payloads/QuestionGroup.cs
こちらに、UniqueCode というのを追加したい。ランダム６桁の英数字で、デフォルトでランダムを生成してください。
現在アクティブな全てのQuestionGroupと被らないものを追加可能

EsCQRSQuestions/EsCQRSQuestions.Domain/Aggregates/QuestionGroups/Commands/CreateQuestionGroup.cs
に項目として追加してください。このコマンドでは基本的にイベントを追加するだけでいいが

EsCQRSQuestions/EsCQRSQuestions.Domain/Workflows/QuestionDisplayWorkflow.cs
このワークフローで重複チェックをしてください。

https://github.com/tomohisa/EsStudy0314/blob/16f0fcaec7d6d6d01da656664b9f07854515edd1/EsCQRSQuestions/EsCQRSQuestions.ApiService/Program.cs#L346-L347
ここで呼ぶところは、重複チェックをしたワークフローを読んでください。

まずはドメインないで機能を作り

EsCQRSQuestions/EsCQRSQuestions.ApiService/Program.cs
ここにAPI作ります。

EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor
のグループ内で表示します。

ただ、このタスクでは計画するだけです。
このファイルの下部に計画をよく考えて追記で記入してください。必要なファイルの読み込みなど調査を行い、できるだけ具体的に計画してください。

チャットではなく、以下の新しいファイル

clinerules_bank/tasks/014_unique_id.sonnet.md

に現在の設計を書いてください。



