using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;

[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public record GetQuestionGroupByGroupIdQuery(Guid QuestionGroupId) :
    IMultiProjectionQuery<GenericTagMultiProjector<QuestionGroupProjector, QuestionGroupTag>,
        GetQuestionGroupByGroupIdQuery,
        QuestionGroup>,
    IWaitForSortableUniqueId
{
    public string? WaitForSortableUniqueId { get; init; }

    public static ResultBox<QuestionGroup> HandleQuery(
        GenericTagMultiProjector<QuestionGroupProjector, QuestionGroupTag> projection,
        GetQuestionGroupByGroupIdQuery query,
        IQueryContext context)
    {
        var item = projection.GetCurrentTagStates().Values
            .Where(m => m.Payload is QuestionGroup)
            .Select(m => (QuestionGroup)m.Payload)
            .FirstOrDefault(m => m.GroupId == query.QuestionGroupId);

        return item is null
            ? ResultBox.FromException<QuestionGroup>(new InvalidOperationException("QuestionGroup not found"))
            : ResultBox.FromValue(item);
    }
}
