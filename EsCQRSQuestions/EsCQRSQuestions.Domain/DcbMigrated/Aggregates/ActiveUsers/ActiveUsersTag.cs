using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.DcbTags;

public record ActiveUsersTag(Guid ActiveUsersId) : IGuidTagGroup<ActiveUsersTag>
{
    public bool IsConsistencyTag() => true;
    public static string TagGroupName => "ActiveUsers";
    public static ActiveUsersTag FromContent(string content) => new(Guid.Parse(content));
    public Guid GetId() => ActiveUsersId;
}
