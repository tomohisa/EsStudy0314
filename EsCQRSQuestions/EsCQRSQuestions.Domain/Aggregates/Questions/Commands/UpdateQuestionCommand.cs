using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record UpdateQuestionCommand(
    Guid QuestionId,
    string Text,
    List<QuestionOption> Options,
    bool AllowMultipleResponses = false // 追加：複数回答を許可するかどうか
) : ICommandWithHandler<UpdateQuestionCommand, QuestionProjector, Question>
{
    public PartitionKeys SpecifyPartitionKeys(UpdateQuestionCommand command) => 
        PartitionKeys.Existing<QuestionProjector>(command.QuestionId);

    public ResultBox<EventOrNone> Handle(UpdateQuestionCommand command, ICommandContext<Question> context)
    {
        // Get the current state of the question
        var aggregate = context.GetAggregate().GetValue();
        var question = aggregate.Payload;
        
        // Validate the command
        if (string.IsNullOrWhiteSpace(command.Text))
        {
            return new ArgumentException("質問テキストは空にできません");
        }
        
        if (command.Options == null || command.Options.Count < 2)
        {
            return new ArgumentException("質問には少なくとも2つの選択肢が必要です");
        }
        
        // Check for duplicate option IDs
        var optionIds = command.Options.Select(o => o.Id).ToList();
        if (optionIds.Count != optionIds.Distinct().Count())
        {
            return new ArgumentException("選択肢のIDは重複できません");
        }
        
        // Cannot update a question that is currently being displayed
        if (question.IsDisplayed)
        {
            return new InvalidOperationException("表示中の質問は更新できません。表示を停止してから編集してください。");
        }
        
        // Create the event
        return EventOrNone.Event(new QuestionUpdated(command.Text, command.Options, command.AllowMultipleResponses));
    }
}
