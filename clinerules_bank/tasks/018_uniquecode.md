                await hubConnection.InvokeAsync("JoinAsSurveyParticipant", UniqueCode);


EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Services/QuestionHubService.cs
にUniqueCodeを取る機能を追加したい。

UniqueCodeを追加したときに、対象のQuestionGroup