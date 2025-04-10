using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using ResultBoxes;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Commands;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using System;
using System.Collections.Generic;
using Sekiban.Pure.Aggregates; // Required for ICommandContext

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands
{
    /// <summary>
    /// Command to create a new question group.
    /// </summary>
    [GenerateSerializer]
    public record CreateQuestionGroupCommand(string Name, List<Guid>? InitialQuestionIds = null)
        : ICommandWithHandler<CreateQuestionGroupCommand, QuestionGroupProjector>
    {
        public PartitionKeys SpecifyPartitionKeys(CreateQuestionGroupCommand command)
            => PartitionKeys.Generate<QuestionGroupProjector>(); // Generates a new Aggregate ID

        public ResultBox<EventOrNone> Handle(CreateQuestionGroupCommand command, ICommandContext<IAggregatePayload> context)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(command.Name))
            {
                return new ArgumentException("Group name cannot be empty.", nameof(command.Name));
            }

            // Ensure we are creating a new aggregate (current state is EmptyAggregatePayload)
            if (context.GetAggregate().GetPayload() is not EmptyAggregatePayload)
            {
                return new InvalidOperationException("Cannot create a group that already exists.");
            }

            var newGroupId = context.GetAggregate().PartitionKeys.AggregateId;

            return EventOrNone.Event(new QuestionGroupCreated(
                newGroupId,
                command.Name,
                command.InitialQuestionIds ?? new List<Guid>()
            ));
        }
    }
}
