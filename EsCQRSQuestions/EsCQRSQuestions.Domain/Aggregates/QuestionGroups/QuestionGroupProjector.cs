using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Events;
using Sekiban.Pure.Projectors;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.ValueObjects;
using System.Collections.Generic;
using System.Linq;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups;

public class QuestionGroupProjector : IAggregateProjector
{
    public IAggregatePayload Project(IAggregatePayload payload, IEvent ev)
        => (payload, ev.GetPayload()) switch
        {
            // 初期状態から QuestionGroup を作成
            (EmptyAggregatePayload, QuestionGroupCreated e) =>
                new QuestionGroup(e.Name, new List<QuestionReference>()),
            
            // グループ名の更新
            (QuestionGroup group, QuestionGroupNameUpdated e) =>
                group with { Name = e.Name },
            
            // 質問の追加
            (QuestionGroup group, QuestionAddedToGroup e) => AddQuestionToGroup(group, e),
            
            // 質問の削除
            (QuestionGroup group, QuestionRemovedFromGroup e) => RemoveQuestionFromGroup(group, e),
            
            // 質問の順序変更
            (QuestionGroup group, QuestionOrderChanged e) => ChangeQuestionOrder(group, e),
            
            // グループの削除
            (QuestionGroup group, QuestionGroupDeleted _) =>
                new DeletedQuestionGroup(group.Name, group.Questions),
            
            // その他の場合はペイロードをそのまま返す
            _ => payload
        };

    private static QuestionGroup AddQuestionToGroup(QuestionGroup group, QuestionAddedToGroup e)
    {
        var questions = new List<QuestionReference>(group.Questions);
        questions.Add(new QuestionReference(e.QuestionId, e.Order));
        // 順序に基づいて並べ替え
        return group with { Questions = questions.OrderBy(q => q.Order).ToList() };
    }

    private static QuestionGroup RemoveQuestionFromGroup(QuestionGroup group, QuestionRemovedFromGroup e)
    {
        var questions = group.Questions.Where(q => q.QuestionId != e.QuestionId).ToList();
        return group with { Questions = questions };
    }

    private static QuestionGroup ChangeQuestionOrder(QuestionGroup group, QuestionOrderChanged e)
    {
        var questions = new List<QuestionReference>(group.Questions);
        var index = questions.FindIndex(q => q.QuestionId == e.QuestionId);
        if (index >= 0)
        {
            questions[index] = questions[index] with { Order = e.NewOrder };
        }
        return group with { Questions = questions.OrderBy(q => q.Order).ToList() };
    }
}