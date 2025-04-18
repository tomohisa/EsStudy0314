using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Events;
using Sekiban.Pure.Projectors;

namespace EsCQRSQuestions.Domain.Aggregates.Questions;

public class QuestionProjector : IAggregateProjector
{
    public IAggregatePayload Project(IAggregatePayload payload, IEvent ev)
        => (payload, ev.GetPayload()) switch
        {
            // Create a new question
            (EmptyAggregatePayload, QuestionCreated created) => new Question(
                created.Text,
                created.Options,
                false,
                new List<QuestionResponse>(),
                created.QuestionGroupId),
            
            // Update an existing question
            (Question question, QuestionUpdated updated) => question with
            {
                Text = updated.Text,
                Options = updated.Options
            },
            
            // Start displaying a question
            (Question question, QuestionDisplayStarted _) => question with
            {
                IsDisplayed = true
            },
            
            // Stop displaying a question
            (Question question, QuestionDisplayStopped _) => question with
            {
                IsDisplayed = false
            },
            
            // Add a response to a question
            (Question question, ResponseAdded response) => question with
            {
                Responses = question.Responses.Append(new QuestionResponse(
                    response.ResponseId,
                    response.ParticipantName,
                    response.SelectedOptionId,
                    response.Comment,
                    response.Timestamp,
                    response.ClientId)).ToList()
            },
            
            // Delete a question
            (Question question, QuestionDeleted _) => new DeletedQuestion(
                question.Text,
                question.Options,
                question.IsDisplayed,
                question.Responses,
                question.QuestionGroupId),
            
            // Update question group ID
            (Question question, QuestionGroupIdUpdated updated) => question with
            {
                QuestionGroupId = updated.QuestionGroupId
            },
            
            // Default case - return the payload unchanged
            _ => payload
        };
}
