using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.DcbTags;

public record QuestionTag(Guid QuestionId) : IGuidTagGroup<QuestionTag>
{
    public bool IsConsistencyTag() => true;
    public static string TagGroupName => "Question";
    public static QuestionTag FromContent(string content) => new(Guid.Parse(content));
    public Guid GetId() => QuestionId;
}
