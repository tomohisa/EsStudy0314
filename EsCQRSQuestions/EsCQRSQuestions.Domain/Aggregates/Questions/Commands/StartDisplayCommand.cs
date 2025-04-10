using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record StartDisplayCommand(
    Guid QuestionId
) : ICommandWithHandler<StartDisplayCommand, QuestionProjector, Question>
{
    public PartitionKeys SpecifyPartitionKeys(StartDisplayCommand command) => 
        PartitionKeys.Existing<QuestionProjector>(command.QuestionId);

    public ResultBox<EventOrNone> Handle(StartDisplayCommand command, ICommandContext<Question> context)
    {
        // Get the current state of the question
        var aggregate = context.GetAggregate().GetValue();
        var question = aggregate.Payload;
        
        // Cannot start displaying a question that is already being displayed
        if (question.IsDisplayed)
        {
            return new InvalidOperationException("Question is already being displayed");
        }
        
        // Create the event
        return EventOrNone.Event(new QuestionDisplayStarted());
    }
}
