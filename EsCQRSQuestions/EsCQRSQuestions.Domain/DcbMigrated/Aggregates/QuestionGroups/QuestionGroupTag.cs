using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.DcbTags;

public record QuestionGroupTag(Guid GroupId) : IGuidTagGroup<QuestionGroupTag>
{
    public bool IsConsistencyTag() => true;
    public static string TagGroupName => "QuestionGroup";
    public static QuestionGroupTag FromContent(string content) => new(Guid.Parse(content));
    public Guid GetId() => GroupId;
}
