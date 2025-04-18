using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Projectors;
using Sekiban.Pure.Query;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer]
public record QuestionDetailQuery(Guid QuestionId)
    : IMultiProjectionQuery<AggregateListProjector<QuestionProjector>, QuestionDetailQuery, QuestionDetailQuery.QuestionDetailRecord>
{
    public static ResultBox<QuestionDetailRecord> HandleQuery(
        MultiProjectionState<AggregateListProjector<QuestionProjector>> projection,
        QuestionDetailQuery query,
        IQueryContext context)
    {
        var aggregateResult = projection.Payload.Aggregates
            .Where(m => m.Key.AggregateId == query.QuestionId)
            .Select(m => m.Value)
            .FirstOrDefault();

        if (aggregateResult == null)
        {
            return new QuestionDetailRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                false,
                new List<ResponseRecord>(), Guid.Empty);
        }

        var question = aggregateResult.GetPayload() as Question;
        if (question == null)
        {
            return new QuestionDetailRecord(
                Guid.Empty,
                string.Empty,
                new List<QuestionOption>(),
                false,
                new List<ResponseRecord>(), Guid.Empty);
        }

        return new QuestionDetailRecord(
            aggregateResult.PartitionKeys.AggregateId,
            question.Text,
            question.Options,
            question.IsDisplayed,
            question.Responses.Select(r => new ResponseRecord(
                r.Id,
                r.ParticipantName,
                r.SelectedOptionId,
                r.Comment,
                r.Timestamp)).ToList(),question.QuestionGroupId);
    }

    [GenerateSerializer]
    public record QuestionDetailRecord(
        Guid QuestionId,
        string Text,
        List<QuestionOption> Options,
        bool IsDisplayed,
        List<ResponseRecord> Responses,
        Guid QuestionGroupId
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
