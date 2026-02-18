using EsCQRSQuestions.Domain.Projections.Questions;
using ResultBoxes;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Queries;

[GenerateSerializer]
public record QuestionGroupExistsQuery(string UniqueCode) :
    IMultiProjectionQuery<QuestionsMultiProjector, QuestionGroupExistsQuery, bool>
{
    public static ResultBox<bool> HandleQuery(
        QuestionsMultiProjector projection,
        QuestionGroupExistsQuery query,
        IQueryContext context)
    {
        var exists = projection.QuestionGroups.Values
            .Any(g => (g.UniqueCode ?? string.Empty).Equals(query.UniqueCode, StringComparison.OrdinalIgnoreCase));

        return ResultBox.FromValue(exists);
    }
}
