using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record UpdateQuestionCommand(
    Guid QuestionId,
    string Text,
    List<QuestionOption> Options
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
            return new ArgumentException("Question text cannot be empty");
        }
        
        if (command.Options == null || command.Options.Count < 2)
        {
            return new ArgumentException("Question must have at least two options");
        }
        
        // Check for duplicate option IDs
        var optionIds = command.Options.Select(o => o.Id).ToList();
        if (optionIds.Count != optionIds.Distinct().Count())
        {
            return new ArgumentException("Option IDs must be unique");
        }
        
        // Cannot update a question that is currently being displayed
        if (question.IsDisplayed)
        {
            return new InvalidOperationException("Cannot update a question that is currently being displayed");
        }
        
        // Create the event
        return EventOrNone.Event(new QuestionUpdated(command.Text, command.Options));
    }
}
