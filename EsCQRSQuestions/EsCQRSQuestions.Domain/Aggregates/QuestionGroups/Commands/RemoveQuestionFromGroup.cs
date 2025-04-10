using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using ResultBoxes;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record RemoveQuestionFromGroup(Guid QuestionGroupId, Guid QuestionId) : 
    ICommandWithHandler<RemoveQuestionFromGroup, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(RemoveQuestionFromGroup command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(RemoveQuestionFromGroup command, ICommandContext<QuestionGroup> context)
        => context.GetAggregate()
            .Conveyor(aggregate => aggregate.Payload.Questions.Any(q => q.QuestionId == command.QuestionId)
                ? EventOrNone.Event(new QuestionRemovedFromGroup(command.QuestionGroupId, command.QuestionId))
                : new ArgumentException($"Question {command.QuestionId} is not in group"));
}
