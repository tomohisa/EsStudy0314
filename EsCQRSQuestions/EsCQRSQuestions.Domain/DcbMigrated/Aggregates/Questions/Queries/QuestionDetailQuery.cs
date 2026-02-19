using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Projections.Questions;
using ResultBoxes;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer]
public record QuestionDetailQuery(Guid QuestionId) :
    IMultiProjectionQuery<QuestionsMultiProjector, QuestionDetailQuery,
        QuestionDetailQuery.QuestionDetailRecord>
{
    public static ResultBox<QuestionDetailRecord> HandleQuery(
        QuestionsMultiProjector projection,
        QuestionDetailQuery query,
        IQueryContext context)
    {
        var question = projection.Questions.TryGetValue(query.QuestionId, out var foundQuestion)
            ? foundQuestion
            : null;

        if (question is null)
        {
            return new QuestionDetailRecord(Guid.Empty, string.Empty, new List<QuestionOption>(), false,
                new List<ResponseRecord>(), Guid.Empty);
        }

        return new QuestionDetailRecord(
            question.QuestionId,
            question.Text,
            question.Options,
            question.IsDisplayed,
            question.Responses.Select(r => new ResponseRecord(r.Id, r.ParticipantName, r.SelectedOptionId, r.Comment,
                r.Timestamp, r.ClientId)).ToList(),
            question.QuestionGroupId);
    }

    [GenerateSerializer]
    public record QuestionDetailRecord(
        Guid QuestionId,
        string Text,
        List<QuestionOption> Options,
        bool IsDisplayed,
        List<ResponseRecord> Responses,
        Guid QuestionGroupId);

    [GenerateSerializer]
    public record ResponseRecord(
        Guid Id,
        string? ParticipantName,
        string SelectedOptionId,
        string? Comment,
        DateTime Timestamp,
        string ClientId);
}
