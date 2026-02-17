using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;

[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public record GetQuestionGroupsQuery :
    IMultiProjectionListQuery<GenericTagMultiProjector<QuestionGroupProjector, QuestionGroupTag>, GetQuestionGroupsQuery,
        GetQuestionGroupsQuery.ResultRecord>,
    IWaitForSortableUniqueId
{
    public string? WaitForSortableUniqueId { get; init; }

    public int? PageSize { get; init; }
    public int? PageNumber { get; init; }

    public static ResultBox<IEnumerable<ResultRecord>> HandleFilter(
        GenericTagMultiProjector<QuestionGroupProjector, QuestionGroupTag> projection,
        GetQuestionGroupsQuery query,
        IQueryContext context)
    {
        return projection.GetCurrentTagStates().Values
            .Where(m => m.Payload is QuestionGroup)
            .Select(m => (QuestionGroup)m.Payload)
            .Select(item => new ResultRecord(
                item.GroupId,
                item.Name,
                item.UniqueCode,
                item.Questions.Select(q => new QuestionReferenceRecord(q.QuestionId, q.Order)).ToList()))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<ResultRecord>> HandleSort(
        IEnumerable<ResultRecord> filteredList,
        GetQuestionGroupsQuery query,
        IQueryContext context) => filteredList.OrderBy(m => m.Name).AsEnumerable().ToResultBox();

    [GenerateSerializer]
    public record ResultRecord(Guid Id, string Name, string UniqueCode, List<QuestionReferenceRecord> Questions);

    [GenerateSerializer]
    public record QuestionReferenceRecord(Guid QuestionId, int Order);
}
