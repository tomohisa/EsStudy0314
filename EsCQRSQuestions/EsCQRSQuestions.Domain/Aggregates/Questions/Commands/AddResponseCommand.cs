using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record AddResponseCommand(
    Guid QuestionId,
    string? ParticipantName,
    string SelectedOptionId,
    string? Comment,
    string ClientId
) : ICommandWithHandler<AddResponseCommand, QuestionProjector, Question>
{
    public PartitionKeys SpecifyPartitionKeys(AddResponseCommand command) => 
        PartitionKeys.Existing<QuestionProjector>(command.QuestionId);

    public ResultBox<EventOrNone> Handle(AddResponseCommand command, ICommandContext<Question> context)
    {
        // Get the current state of the question
        var question = context.GetAggregate().GetValue().Payload;
        
        // Cannot add a response to a question that is not being displayed
        if (!question.IsDisplayed)
        {
            return new InvalidOperationException("Cannot add a response to a question that is not being displayed");
        }
        
        // Validate the selected option ID
        if (string.IsNullOrWhiteSpace(command.SelectedOptionId))
        {
            return new ArgumentException("Selected option ID cannot be empty");
        }
        
        // Check if the selected option ID exists
        if (!question.Options.Any(o => o.Id == command.SelectedOptionId))
        {
            return new ArgumentException($"Option with ID '{command.SelectedOptionId}' does not exist");
        }
        
        // 複数回答が許可されていない場合、同じクライアントからの回答がすでに存在するかチェック
        if (!question.AllowMultipleResponses && 
            question.Responses.Any(r => r.ClientId == command.ClientId))
        {
            return new InvalidOperationException("Multiple responses are not allowed for this question");
        }
        
        // Create the event
        return EventOrNone.Event(new ResponseAdded(
            Guid.NewGuid(),
            command.ParticipantName,
            command.SelectedOptionId,
            command.Comment,
            DateTime.UtcNow,
            command.ClientId));
    }
}
