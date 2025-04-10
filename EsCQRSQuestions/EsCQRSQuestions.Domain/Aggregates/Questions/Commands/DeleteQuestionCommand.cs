using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record DeleteQuestionCommand(
    Guid QuestionId
) : ICommandWithHandler<DeleteQuestionCommand, QuestionProjector, Question>
{
    public PartitionKeys SpecifyPartitionKeys(DeleteQuestionCommand command) => 
        PartitionKeys.Existing<QuestionProjector>(command.QuestionId);

    public ResultBox<EventOrNone> Handle(DeleteQuestionCommand command, ICommandContext<Question> context)
    {
        // Get the current state of the question
        var aggregate = context.GetAggregate().GetValue();
        var question = aggregate.Payload;
        
        // Cannot delete a question that is currently being displayed
        if (question.IsDisplayed)
        {
            return new InvalidOperationException("Cannot delete a question that is currently being displayed");
        }
        
        // Create the event
        return EventOrNone.Event(new QuestionDeleted());
    }
}
