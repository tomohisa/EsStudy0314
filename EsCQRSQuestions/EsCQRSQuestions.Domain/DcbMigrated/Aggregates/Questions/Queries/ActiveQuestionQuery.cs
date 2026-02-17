using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer]
public record ActiveQuestionQuery(Guid QuestionGroupId) :
    IMultiProjectionQuery<GenericTagMultiProjector<QuestionProjector, QuestionTag>, ActiveQuestionQuery,
        ActiveQuestionQuery.ActiveQuestionRecord>
{
    public static ResultBox<ActiveQuestionRecord> HandleQuery(
        GenericTagMultiProjector<QuestionProjector, QuestionTag> projection,
        ActiveQuestionQuery query,
        IQueryContext context)
    {
        var activeQuestion = projection.GetCurrentTagStates().Values
            .Where(m => m.Payload is Question q && q.QuestionGroupId == query.QuestionGroupId && q.IsDisplayed)
            .Select(m => (Question)m.Payload)
            .Select(item => new ActiveQuestionRecord(
                item.QuestionId,
                item.Text,
                item.Options,
                item.Responses.Select(r => new ResponseRecord(r.Id, r.ParticipantName, r.SelectedOptionId, r.Comment,
                    r.Timestamp, r.ClientId)).ToList(),
                item.QuestionGroupId,
                item.AllowMultipleResponses))
            .FirstOrDefault();

        return activeQuestion ?? new ActiveQuestionRecord(Guid.Empty, string.Empty, new List<QuestionOption>(),
            new List<ResponseRecord>(), Guid.Empty, false);
    }

    [GenerateSerializer]
    public record ActiveQuestionRecord(
        Guid QuestionId,
        string Text,
        List<QuestionOption> Options,
        List<ResponseRecord> Responses,
        Guid QuestionGroupId,
        bool AllowMultipleResponses = false);

    [GenerateSerializer]
    public record ResponseRecord(
        Guid Id,
        string? ParticipantName,
        string SelectedOptionId,
        string? Comment,
        DateTime Timestamp,
        string ClientId);
}
