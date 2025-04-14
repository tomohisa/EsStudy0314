using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Aggregates;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record CreateQuestionGroup(
    string Name, 
    string UniqueCode = "") : ICommandWithHandler<CreateQuestionGroup, QuestionGroupProjector>
{
    public PartitionKeys SpecifyPartitionKeys(CreateQuestionGroup command) => 
        PartitionKeys.Generate<QuestionGroupProjector>();

    public ResultBox<EventOrNone> Handle(CreateQuestionGroup command, ICommandContext<IAggregatePayload> context)
        => context.GetAggregate()
            .Conveyor(aggregate => {
                var groupId = aggregate.PartitionKeys.AggregateId;
                // ユニークコードを生成または使用
                string uniqueCode = string.IsNullOrEmpty(command.UniqueCode) ? 
                    GenerateRandomCode() : command.UniqueCode;
                
                // イベント生成 - デフォルト値を持つパラメータの場合でも明示的に渡しておく
                return EventOrNone.Event(new QuestionGroupCreated(
                    groupId, command.Name, uniqueCode));
            });
            
    private static string GenerateRandomCode()
    {
        // 英数字からランダムに6文字を選択
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
