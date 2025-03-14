using EsCQRSQuestions.Domain.Aggregates.Questions;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Projectors;
using Sekiban.Pure.Query;
using System.Diagnostics.CodeAnalysis;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer]
public record ActiveQuestionQuery()
    : IMultiProjectionQuery<AggregateListProjector<QuestionProjector>, ActiveQuestionQuery, ActiveQuestionQuery.ActiveQuestionRecord>
{
    public static ResultBox<ActiveQuestionRecord> HandleQuery(
        MultiProjectionState<AggregateListProjector<QuestionProjector>> projection,
        ActiveQuestionQuery query,
        IQueryContext context)
    {
        var activeQuestion = projection.Payload.Aggregates
            .Where(m => m.Value.GetPayload() is Question)
            .Select(m => ((Question)m.Value.GetPayload(), m.Value.PartitionKeys))
            .Where(tuple => tuple.Item1.IsDisplayed)
            .Select(tuple => new ActiveQuestionRecord(
                tuple.PartitionKeys.AggregateId,
                tuple.Item1.Text,
                tuple.Item1.Options,
                tuple.Item1.Responses.Select(r => new ResponseRecord(
                    r.Id,
                    r.ParticipantName,
                    r.SelectedOptionId,
                    r.Comment,
                    r.Timestamp)).ToList()))
            .FirstOrDefault();

        return activeQuestion != null 
            ? activeQuestion.ToResultBox() 
            : new ActiveQuestionRecord(
                Guid.Empty, 
                string.Empty, 
                new List<QuestionOption>(), 
                new List<ResponseRecord>()).ToResultBox();
    }

    [GenerateSerializer]
    public record ActiveQuestionRecord(
        Guid QuestionId,
        string Text,
        List<QuestionOption> Options,
        List<ResponseRecord> Responses
    );

    [GenerateSerializer]
    public record ResponseRecord(
        Guid Id,
        string? ParticipantName,
        string SelectedOptionId,
        string? Comment,
        DateTime Timestamp
    );
}
