using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using Sekiban.Dcb.Events;
using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.Aggregates.Questions;

public class QuestionProjector : ITagProjector<QuestionProjector>
{
    public static string ProjectorVersion => "1.0.0";
    public static string ProjectorName => nameof(QuestionProjector);

    public static ITagStatePayload Project(ITagStatePayload current, Event ev) =>
        (current, ev.Payload) switch
        {
            (EmptyTagStatePayload, QuestionCreated created) => new Question(
                created.QuestionId,
                created.Text,
                created.Options,
                false,
                new List<QuestionResponse>(),
                created.QuestionGroupId,
                created.AllowMultipleResponses),
            (Question question, QuestionUpdated updated) => question with
            {
                Text = updated.Text,
                Options = updated.Options,
                AllowMultipleResponses = updated.AllowMultipleResponses
            },
            (Question question, QuestionDisplayStarted) => question with { IsDisplayed = true },
            (Question question, QuestionDisplayStopped) => question with { IsDisplayed = false },
            (Question question, QuestionGroupIdUpdated updated) => question with { QuestionGroupId = updated.QuestionGroupId },
            (Question question, ResponseAdded response) => question with
            {
                Responses = question.Responses
                    .Append(new QuestionResponse(response.ResponseId, response.ParticipantName, response.SelectedOptionId,
                        response.Comment, response.Timestamp, response.ClientId)).ToList()
            },
            (Question question, QuestionDeleted) => new DeletedQuestion(question.Text, question.Options,
                question.IsDisplayed, question.Responses, question.QuestionGroupId, question.AllowMultipleResponses),
            _ => current
        };
}
