using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Aggregates;
using ResultBoxes;
using System;
using System.Linq;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record AddQuestionToGroup(Guid QuestionGroupId, Guid QuestionId, int Order) : 
    ICommandWithHandler<AddQuestionToGroup, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(AddQuestionToGroup command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(AddQuestionToGroup command, ICommandContext<QuestionGroup> context)
        => context.GetAggregate()
            .Conveyor(aggregate => aggregate.Payload.Questions.Any(q => q.QuestionId == command.QuestionId)
                ? new ArgumentException($"Question {command.QuestionId} is already in group")
                : EventOrNone.Event(new QuestionAddedToGroup(command.QuestionId, command.Order)));
}