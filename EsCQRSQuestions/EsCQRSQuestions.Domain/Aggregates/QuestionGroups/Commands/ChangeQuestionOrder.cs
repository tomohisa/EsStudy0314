using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using System.Linq;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record ChangeQuestionOrder(Guid QuestionGroupId, Guid QuestionId, int NewOrder) : 
    ICommandWithHandler<ChangeQuestionOrder, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(ChangeQuestionOrder command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(ChangeQuestionOrder command, ICommandContext<QuestionGroup> context)
        => context.GetAggregate()
            .Conveyor(aggregate => {
                if (!aggregate.Payload.Questions.Any(q => q.QuestionId == command.QuestionId))
                {
                    return new ArgumentException($"Question {command.QuestionId} is not in group");
                }
                
                // Calculate the new order of all questions
                var questions = aggregate.Payload.Questions.ToList();
                var questionToMove = questions.First(q => q.QuestionId == command.QuestionId);
                
                // Remove the question from its current position
                questions.Remove(questionToMove);
                
                // Create a new question with the updated order
                var updatedQuestion = questionToMove with { Order = command.NewOrder };
                
                // Insert at appropriate position based on new order
                var insertIndex = 0;
                while (insertIndex < questions.Count && questions[insertIndex].Order <= command.NewOrder)
                {
                    insertIndex++;
                }
                questions.Insert(insertIndex, updatedQuestion);
                
                // Re-number all questions to ensure continuous ordering
                for (var i = 0; i < questions.Count; i++)
                {
                    questions[i] = questions[i] with { Order = i };
                }
                
                // Extract just the QuestionIds in their new order
                var updatedOrder = questions.OrderBy(q => q.Order).Select(q => q.QuestionId).ToList();
                
                return EventOrNone.Event(new QuestionOrderChanged(
                    command.QuestionGroupId, 
                    command.QuestionId, 
                    command.NewOrder, 
                    updatedOrder
                ));
            });
}
