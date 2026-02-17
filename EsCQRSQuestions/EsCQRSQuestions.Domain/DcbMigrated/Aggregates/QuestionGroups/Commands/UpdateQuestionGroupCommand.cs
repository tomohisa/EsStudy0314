using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record UpdateQuestionGroupCommand(Guid GroupId, string NewName) : ICommandWithHandler<UpdateQuestionGroupCommand>
{
    public static async Task<ResultBox<EventOrNone>> HandleAsync(UpdateQuestionGroupCommand command, ICommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.NewName))
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("Group name cannot be empty.", nameof(command.NewName)));
        }

        var tag = new QuestionGroupTag(command.GroupId);
        var stateResult = await context.GetStateAsync<QuestionGroupProjector>(tag);
        if (!stateResult.IsSuccess || stateResult.GetValue().Payload is not QuestionGroup group)
        {
            return ResultBox.FromException<EventOrNone>(new ArgumentException("QuestionGroup not found"));
        }

        return group.Name == command.NewName
            ? EventOrNone.None
            : EventOrNone.EventWithTags(new QuestionGroupUpdated(command.GroupId, command.NewName), tag);
    }
}
