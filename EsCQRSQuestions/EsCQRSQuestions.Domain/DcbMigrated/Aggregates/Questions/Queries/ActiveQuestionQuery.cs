using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using EsCQRSQuestions.Domain.Projections.Questions;
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

[GenerateSerializer]
public record ActiveQuestionByUniqueCodeQuery(string UniqueCode) :
    IMultiProjectionQuery<QuestionsMultiProjector, ActiveQuestionByUniqueCodeQuery, ActiveQuestionQuery.ActiveQuestionRecord>
{
    public static ResultBox<ActiveQuestionQuery.ActiveQuestionRecord> HandleQuery(
        QuestionsMultiProjector projection,
        ActiveQuestionByUniqueCodeQuery query,
        IQueryContext context)
    {
        if (string.IsNullOrWhiteSpace(query.UniqueCode))
        {
            return new ActiveQuestionQuery.ActiveQuestionRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                new List<ActiveQuestionQuery.ResponseRecord>(),
                Guid.Empty,
                false);
        }

        var normalizedCode = query.UniqueCode.Trim();
        var group = projection.QuestionGroups.Values
            .FirstOrDefault(g => string.Equals(g.UniqueCode, normalizedCode, StringComparison.OrdinalIgnoreCase));

        if (group is null)
        {
            return new ActiveQuestionQuery.ActiveQuestionRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                new List<ActiveQuestionQuery.ResponseRecord>(),
                Guid.Empty,
                false);
        }

        var activeQuestion = group.Questions
            .OrderBy(q => q.Order)
            .Select(qr => projection.Questions.TryGetValue(qr.QuestionId, out var q) ? q : null)
            .Where(q => q is not null)
            .Select(q => q!)
            .FirstOrDefault(q => q.IsDisplayed);

        if (activeQuestion is null)
        {
            return new ActiveQuestionQuery.ActiveQuestionRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                new List<ActiveQuestionQuery.ResponseRecord>(),
                group.GroupId,
                false);
        }

        return new ActiveQuestionQuery.ActiveQuestionRecord(
            activeQuestion.QuestionId,
            activeQuestion.Text,
            activeQuestion.Options ?? new List<QuestionOption>(),
            (activeQuestion.Responses ?? new List<QuestionResponse>())
                .Select(r => new ActiveQuestionQuery.ResponseRecord(
                    r.Id,
                    r.ParticipantName,
                    r.SelectedOptionId,
                    r.Comment,
                    r.Timestamp,
                    r.ClientId))
                .ToList(),
            group.GroupId,
            activeQuestion.AllowMultipleResponses);
    }
}
