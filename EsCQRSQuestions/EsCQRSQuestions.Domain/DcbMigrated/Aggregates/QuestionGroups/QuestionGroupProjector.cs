using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using Sekiban.Dcb.Events;
using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups;

public class QuestionGroupProjector : ITagProjector<QuestionGroupProjector>
{
    public static string ProjectorVersion => "1.0.0";
    public static string ProjectorName => nameof(QuestionGroupProjector);

    public static ITagStatePayload Project(ITagStatePayload current, Event ev) =>
        (current, ev.Payload) switch
        {
            (EmptyTagStatePayload, QuestionGroupCreated created) => new QuestionGroup(
                created.GroupId,
                created.Name,
                created.UniqueCode,
                (created.InitialQuestionIds ?? []).Select((id, i) => new QuestionReference(id, i)).ToList()),
            (QuestionGroup group, QuestionGroupUpdated updated) => group with { Name = updated.NewName },
            (QuestionGroup group, QuestionGroupNameUpdated updated) => group with { Name = updated.Name },
            (QuestionGroup group, QuestionAddedToGroup added) => group with
            {
                Questions = group.Questions.Any(q => q.QuestionId == added.QuestionId)
                    ? group.Questions
                    : group.Questions.Append(new QuestionReference(added.QuestionId, added.Order)).OrderBy(q => q.Order)
                        .ToList()
            },
            (QuestionGroup group, QuestionRemovedFromGroup removed) => group with
            {
                Questions = group.Questions.Where(q => q.QuestionId != removed.QuestionId).OrderBy(q => q.Order)
                    .Select((q, i) => q with { Order = i }).ToList()
            },
            (QuestionGroup group, QuestionOrderChanged orderChanged) => group with
            {
                Questions = orderChanged.UpdatedOrder.Select((id, i) => new QuestionReference(id, i)).ToList()
            },
            (QuestionGroup group, QuestionGroupDeleted) => new DeletedQuestionGroup(group.Name, group.UniqueCode,
                group.Questions, DateTime.UtcNow),
            _ => current
        };
}
