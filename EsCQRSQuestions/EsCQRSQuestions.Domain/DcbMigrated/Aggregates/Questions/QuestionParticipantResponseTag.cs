using System.Security.Cryptography;
using System.Text;
using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.DcbTags;

public record QuestionParticipantResponseTag(Guid QuestionParticipantResponseId) : IGuidTagGroup<QuestionParticipantResponseTag>
{
    public bool IsConsistencyTag() => true;
    public static string TagGroupName => "QuestionParticipantResponse";
    public static QuestionParticipantResponseTag FromContent(string content) => new(Guid.Parse(content));
    public Guid GetId() => QuestionParticipantResponseId;

    public static QuestionParticipantResponseTag Create(Guid questionId, string clientId) =>
        new(CreateDeterministicId(questionId, clientId));

    private static Guid CreateDeterministicId(Guid questionId, string clientId)
    {
        var normalizedClientId = (clientId ?? string.Empty).Trim().ToLowerInvariant();
        var raw = $"{questionId:N}|{normalizedClientId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
