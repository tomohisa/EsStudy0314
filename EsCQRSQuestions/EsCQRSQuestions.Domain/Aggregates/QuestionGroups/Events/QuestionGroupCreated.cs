using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events
{
    /// <summary>
    /// Event payload indicating that a new question group has been created.
    /// </summary>
    [GenerateSerializer]
    public record QuestionGroupCreated(
        Guid GroupId, // Aggregate ID
        string Name,
        string UniqueCode = "", // 新規追加：6桁の英数字（デフォルト値を設定）
        List<Guid>? InitialQuestionIds = null // Optional: If created with initial questions
        ) : IEventPayload;
}
