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
public record StopDisplayCommand(
    Guid QuestionId
) : ICommandWithHandler<StopDisplayCommand, QuestionProjector, Question>
{
    public PartitionKeys SpecifyPartitionKeys(StopDisplayCommand command) => 
        PartitionKeys.Existing<QuestionProjector>(command.QuestionId);

    public ResultBox<EventOrNone> Handle(StopDisplayCommand command, ICommandContext<Question> context)
    {
        // Get the current state of the question
        var aggregate = context.GetAggregate().GetValue();
        var question = aggregate.Payload;
        
        // Cannot stop displaying a question that is not being displayed
        if (!question.IsDisplayed)
        {
            return new InvalidOperationException("Question is not currently being displayed");
        }
        
        // Create the event
        return EventOrNone.Event(new QuestionDisplayStopped());
    }
}
