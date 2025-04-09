using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Aggregates;
using ResultBoxes;
using System;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record UpdateQuestionGroupIdCommand(Guid QuestionId, Guid QuestionGroupId) : 
    ICommandWithHandler<UpdateQuestionGroupIdCommand, QuestionProjector>
{
    public PartitionKeys SpecifyPartitionKeys(UpdateQuestionGroupIdCommand command) => 
        PartitionKeys.Existing<QuestionProjector>(command.QuestionId);

    public ResultBox<EventOrNone> Handle(UpdateQuestionGroupIdCommand command, ICommandContext<IAggregatePayload> context)
    {
        // Validate QuestionGroupId
        if (command.QuestionGroupId == Guid.Empty)
        {
            return new ArgumentException("QuestionGroupId is required");
        }

        return EventOrNone.Event(new QuestionGroupIdUpdated(command.QuestionGroupId));
    }
}