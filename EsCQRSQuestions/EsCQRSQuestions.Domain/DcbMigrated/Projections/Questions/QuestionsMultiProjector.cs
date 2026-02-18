using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Dcb;
using Sekiban.Dcb.Common;
using Sekiban.Dcb.Domains;
using Sekiban.Dcb.Events;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Tags;
using System.Collections.Immutable;

namespace EsCQRSQuestions.Domain.Projections.Questions;

[GenerateSerializer]
public record QuestionsMultiProjector(
    ImmutableDictionary<Guid, QuestionsMultiProjector.QuestionGroupInfo> QuestionGroups,
    ImmutableDictionary<Guid, QuestionsMultiProjector.QuestionInfo> Questions
) : IMultiProjector<QuestionsMultiProjector>
{
    public QuestionsMultiProjector() : this(
        ImmutableDictionary<Guid, QuestionGroupInfo>.Empty,
        ImmutableDictionary<Guid, QuestionInfo>.Empty)
    {
    }

    [GenerateSerializer]
    public record QuestionGroupInfo(Guid GroupId, string Name, List<QuestionReference> Questions, string UniqueCode = "");

    [GenerateSerializer]
    public record QuestionInfo(
        Guid QuestionId,
        string Text,
        List<QuestionOption> Options,
        bool IsDisplayed,
        List<QuestionResponse> Responses,
        Guid QuestionGroupId,
        string QuestionGroupName,
        int Order = 0,
        bool AllowMultipleResponses = false);

    public static QuestionsMultiProjector GenerateInitialPayload() =>
        new(ImmutableDictionary<Guid, QuestionGroupInfo>.Empty, ImmutableDictionary<Guid, QuestionInfo>.Empty);

    public static string MultiProjectorName => nameof(QuestionsMultiProjector);
    public static string MultiProjectorVersion => "1.0.0";

    public static ResultBox<QuestionsMultiProjector> Project(
        QuestionsMultiProjector payload,
        Event ev,
        List<ITag> tags,
        DcbDomainTypes domainTypes,
        SortableUniqueId safeWindowThreshold)
    {
        var result = ev.Payload switch
        {
            QuestionGroupCreated e => payload with
            {
                QuestionGroups = payload.QuestionGroups.SetItem(
                    e.GroupId,
                    new QuestionGroupInfo(e.GroupId, e.Name, new List<QuestionReference>(), e.UniqueCode))
            },
            QuestionGroupNameUpdated e => UpdateGroupNameAndRelatedQuestions(payload, GetGroupId(ev, tags), e.Name),
            QuestionGroupDeleted => payload with { QuestionGroups = payload.QuestionGroups.Remove(GetGroupId(ev, tags)) },
            QuestionAddedToGroup e => AddQuestionToGroup(payload, e.GroupId, e.QuestionId),
            QuestionRemovedFromGroup e => RemoveQuestionFromGroup(payload, e.GroupId, e.QuestionId),
            QuestionOrderChanged e => UpdateQuestionOrder(payload, e.GroupId, e.QuestionId, e.NewOrder),
            QuestionCreated e => AddNewQuestion(payload, e.QuestionId, e),
            QuestionUpdated e => UpdateExistingQuestion(payload, GetQuestionId(ev, tags), e),
            QuestionDeleted => payload with { Questions = payload.Questions.Remove(GetQuestionId(ev, tags)) },
            QuestionGroupIdUpdated e => UpdateQuestionGroupId(payload, GetQuestionId(ev, tags), e.QuestionGroupId),
            QuestionDisplayStarted => UpdateQuestionDisplayStatus(payload, GetQuestionId(ev, tags), true),
            QuestionDisplayStopped => UpdateQuestionDisplayStatus(payload, GetQuestionId(ev, tags), false),
            ResponseAdded e => AddResponseToQuestion(payload, GetQuestionId(ev, tags), e),
            _ => payload
        };

        return ResultBox.FromValue(result);
    }

    private static Guid GetQuestionId(Event ev, List<ITag> tags) =>
        tags.OfType<EsCQRSQuestions.Domain.DcbTags.QuestionTag>().Select(t => t.QuestionId).FirstOrDefault();

    private static Guid GetGroupId(Event ev, List<ITag> tags) =>
        tags.OfType<EsCQRSQuestions.Domain.DcbTags.QuestionGroupTag>().Select(t => t.GroupId).FirstOrDefault();

    private static QuestionsMultiProjector UpdateGroupNameAndRelatedQuestions(QuestionsMultiProjector payload, Guid groupId,
        string newName)
    {
        if (!payload.QuestionGroups.TryGetValue(groupId, out var group))
        {
            return payload;
        }

        var updatedGroups = payload.QuestionGroups.SetItem(groupId, group with { Name = newName });

        var updatedQuestions = payload.Questions;
        foreach (var question in payload.Questions.Values.Where(q => q.QuestionGroupId == groupId))
        {
            updatedQuestions = updatedQuestions.SetItem(question.QuestionId, question with { QuestionGroupName = newName });
        }

        return payload with { QuestionGroups = updatedGroups, Questions = updatedQuestions };
    }

    private static QuestionsMultiProjector AddNewQuestion(QuestionsMultiProjector payload, Guid questionId, QuestionCreated e)
    {
        var groupName = "";
        var order = 0;
        if (payload.QuestionGroups.TryGetValue(e.QuestionGroupId, out var group))
        {
            groupName = group.Name;
            var questionRef = group.Questions.FirstOrDefault(q => q.QuestionId == questionId);
            if (questionRef != null)
            {
                order = questionRef.Order;
            }
        }

        var updatedQuestions = payload.Questions.SetItem(questionId,
            new QuestionInfo(questionId, e.Text, e.Options, false, new List<QuestionResponse>(), e.QuestionGroupId,
                groupName, order, e.AllowMultipleResponses));

        return payload with { Questions = updatedQuestions };
    }

    private static QuestionsMultiProjector UpdateExistingQuestion(QuestionsMultiProjector payload, Guid questionId,
        QuestionUpdated e)
    {
        if (!payload.Questions.TryGetValue(questionId, out var question))
        {
            return payload;
        }

        var updatedQuestions = payload.Questions.SetItem(questionId,
            question with { Text = e.Text, Options = e.Options, AllowMultipleResponses = e.AllowMultipleResponses });

        return payload with { Questions = updatedQuestions };
    }

    private static QuestionsMultiProjector UpdateQuestionGroupId(QuestionsMultiProjector payload, Guid questionId,
        Guid newGroupId)
    {
        if (!payload.Questions.TryGetValue(questionId, out var question))
        {
            return payload;
        }

        var newGroupName = "";
        var newOrder = 0;
        if (payload.QuestionGroups.TryGetValue(newGroupId, out var group))
        {
            newGroupName = group.Name;
            var questionRef = group.Questions.FirstOrDefault(q => q.QuestionId == questionId);
            newOrder = questionRef?.Order ?? (group.Questions.Count > 0 ? group.Questions.Max(q => q.Order) + 1 : 0);
        }

        var updatedQuestions = payload.Questions.SetItem(questionId,
            question with { QuestionGroupId = newGroupId, QuestionGroupName = newGroupName, Order = newOrder });

        return payload with { Questions = updatedQuestions };
    }

    private static QuestionsMultiProjector UpdateQuestionDisplayStatus(QuestionsMultiProjector payload, Guid questionId,
        bool isDisplayed)
    {
        if (!payload.Questions.TryGetValue(questionId, out var question))
        {
            return payload;
        }

        var updatedQuestions = payload.Questions.SetItem(questionId, question with { IsDisplayed = isDisplayed });
        return payload with { Questions = updatedQuestions };
    }

    private static QuestionsMultiProjector AddResponseToQuestion(QuestionsMultiProjector payload, Guid questionId,
        ResponseAdded e)
    {
        if (!payload.Questions.TryGetValue(questionId, out var question))
        {
            return payload;
        }

        var responses = question.Responses.ToList();
        responses.Add(new QuestionResponse(e.ResponseId, e.ParticipantName, e.SelectedOptionId, e.Comment, e.Timestamp,
            e.ClientId));

        var updatedQuestions = payload.Questions.SetItem(questionId, question with { Responses = responses });
        return payload with { Questions = updatedQuestions };
    }

    private static QuestionsMultiProjector AddQuestionToGroup(QuestionsMultiProjector payload, Guid groupId, Guid questionId)
    {
        if (!payload.QuestionGroups.TryGetValue(groupId, out var group))
        {
            return payload;
        }

        if (group.Questions.Any(q => q.QuestionId == questionId))
        {
            return payload;
        }

        var newOrder = group.Questions.Count > 0 ? group.Questions.Max(q => q.Order) + 1 : 0;
        var updatedGroupQuestions = group.Questions.ToList();
        updatedGroupQuestions.Add(new QuestionReference(questionId, newOrder));

        var updatedGroups = payload.QuestionGroups.SetItem(groupId, group with { Questions = updatedGroupQuestions });

        var updatedQuestions = payload.Questions;
        if (updatedQuestions.TryGetValue(questionId, out var question))
        {
            updatedQuestions = updatedQuestions.SetItem(questionId,
                question with { QuestionGroupId = groupId, QuestionGroupName = group.Name, Order = newOrder });
        }

        return payload with { QuestionGroups = updatedGroups, Questions = updatedQuestions };
    }

    private static QuestionsMultiProjector RemoveQuestionFromGroup(QuestionsMultiProjector payload, Guid groupId,
        Guid questionId)
    {
        if (!payload.QuestionGroups.TryGetValue(groupId, out var group))
        {
            return payload;
        }

        if (!group.Questions.Any(q => q.QuestionId == questionId))
        {
            return payload;
        }

        var updatedGroupQuestions = group.Questions.Where(q => q.QuestionId != questionId).ToList();
        for (var i = 0; i < updatedGroupQuestions.Count; i++)
        {
            updatedGroupQuestions[i] = updatedGroupQuestions[i] with { Order = i };
        }

        var updatedGroups = payload.QuestionGroups.SetItem(groupId, group with { Questions = updatedGroupQuestions });

        var updatedQuestions = payload.Questions;
        if (updatedQuestions.TryGetValue(questionId, out var question))
        {
            updatedQuestions = updatedQuestions.SetItem(questionId,
                question with { QuestionGroupId = Guid.Empty, QuestionGroupName = "", Order = 0 });
        }

        return payload with { QuestionGroups = updatedGroups, Questions = updatedQuestions };
    }

    private static QuestionsMultiProjector UpdateQuestionOrder(QuestionsMultiProjector payload, Guid groupId, Guid questionId,
        int newOrder)
    {
        if (!payload.QuestionGroups.TryGetValue(groupId, out var group))
        {
            return payload;
        }

        var groupQuestions = group.Questions.ToList();
        var questionRef = groupQuestions.FirstOrDefault(q => q.QuestionId == questionId);
        if (questionRef is null)
        {
            return payload;
        }

        groupQuestions.Remove(questionRef);
        var insertIndex = Math.Min(Math.Max(newOrder, 0), groupQuestions.Count);
        groupQuestions.Insert(insertIndex, questionRef with { Order = newOrder });

        for (var i = 0; i < groupQuestions.Count; i++)
        {
            groupQuestions[i] = groupQuestions[i] with { Order = i };
        }

        var updatedGroups = payload.QuestionGroups.SetItem(groupId, group with { Questions = groupQuestions });

        var updatedQuestions = payload.Questions;
        foreach (var qref in groupQuestions)
        {
            if (updatedQuestions.TryGetValue(qref.QuestionId, out var qInfo))
            {
                updatedQuestions = updatedQuestions.SetItem(qref.QuestionId, qInfo with { Order = qref.Order });
            }
        }

        return payload with { QuestionGroups = updatedGroups, Questions = updatedQuestions };
    }
}
