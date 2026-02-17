using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer]
public record QuestionGroupExistsQuery(string UniqueCode) :
    IMultiProjectionQuery<GenericTagMultiProjector<QuestionGroupProjector, QuestionGroupTag>, QuestionGroupExistsQuery, bool>
{
    public static ResultBox<bool> HandleQuery(
        GenericTagMultiProjector<QuestionGroupProjector, QuestionGroupTag> projection,
        QuestionGroupExistsQuery query,
        IQueryContext context)
    {
        var exists = projection.GetCurrentTagStates().Values
            .Where(m => m.Payload is QuestionGroup)
            .Select(m => (QuestionGroup)m.Payload)
            .Any(g => g.UniqueCode.Equals(query.UniqueCode, StringComparison.OrdinalIgnoreCase));

        return ResultBox.FromValue(exists);
    }
}
