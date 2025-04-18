using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions;
using ResultBoxes;
using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Projectors;
using Sekiban.Pure.Query;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;

[GenerateSerializer]
public record GetQuestionGroupByGroupIdQuery(Guid QuestionGroupId) : 
    IMultiProjectionQuery<AggregateListProjector<QuestionGroupProjector>, GetQuestionGroupByGroupIdQuery, Aggregate<QuestionGroup>>
{
    public static ResultBox<Aggregate<QuestionGroup>> HandleQuery(MultiProjectionState<AggregateListProjector<QuestionGroupProjector>> projection, GetQuestionGroupByGroupIdQuery query,
        IQueryContext context)
    {
        return projection.Payload.Aggregates
            .Where(m => m.Value.GetPayload() is QuestionGroup)
            .Select(m => m.Value.ToTypedPayload<QuestionGroup>().UnwrapBox())
            .First(m => m.PartitionKeys.AggregateId == query.QuestionGroupId)
            .ToResultBox();
    }
}