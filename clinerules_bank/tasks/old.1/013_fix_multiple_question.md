https://github.com/tomohisa/EsStudy0314/blob/bec831b9195c251b70f5f7d8c4ff1c8955d3554e/EsCQRSQuestions/EsCQRSQuestions.AdminWeb/QuestionApiClient.cs#L105-L106
StartDisplayQuestion

ですが、1つがDisplay中にもう1つを Start Displayしたら、2つDisplay中になるのは良くない

https://github.com/tomohisa/EsStudy0314/blob/bec831b9195c251b70f5f7d8c4ff1c8955d3554e/EsCQRSQuestions/EsCQRSQuestions.ApiService/Program.cs#L262-L263

        "/questions/startDisplay",

Should call Worlflow,

Workflow should be
- First Query Question with GroupId
- Stop if anything is On (Started Display)
- Start the one that started

EsCQRSQuestions/EsCQRSQuestions.Domain/Workflows
If you need new workflow, see 
EsCQRSQuestions/EsCQRSQuestions.Domain/Workflows/QuestionGroupWorkflow.cs
and add workflow.

ただ、このタスクでは計画するだけです。
このファイルの下部に計画をよく考えて追記で記入してください。必要なファイルの読み込みなど調査を行い、できるだけ具体的に計画してください。

チャットではなく、以下の新しいファイル

clinerules_bank/tasks/013_fix_multiple_question.cline.md

に現在の設計を書いてください。



