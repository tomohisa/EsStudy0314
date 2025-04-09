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
public record CreateQuestionCommand(
    string Text,
    List<QuestionOption> Options,
    Guid QuestionGroupId
) : ICommandWithHandler<CreateQuestionCommand, QuestionProjector>
{
    public PartitionKeys SpecifyPartitionKeys(CreateQuestionCommand command) => 
        PartitionKeys.Generate<QuestionProjector>();

    public ResultBox<EventOrNone> Handle(CreateQuestionCommand command, ICommandContext<IAggregatePayload> context)
    {
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
        
        // Validate QuestionGroupId
        if (command.QuestionGroupId == Guid.Empty)
        {
            return new ArgumentException("QuestionGroupId is required");
        }
        
        // Create the event
        return EventOrNone.Event(new QuestionCreated(command.Text, command.Options, command.QuestionGroupId));
    }
}
