using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;
using Sekiban.Pure.Events;
using Sekiban.Pure.Projectors;
using System.Collections.Immutable;

namespace EsCQRSQuestions.Domain.Projections.Questions;

[GenerateSerializer]
public record QuestionsMultiProjector(
    ImmutableDictionary<Guid, QuestionsMultiProjector.QuestionGroupInfo> QuestionGroups,
    ImmutableDictionary<Guid, QuestionsMultiProjector.QuestionInfo> Questions
) : IMultiProjector<QuestionsMultiProjector>
{
    // 入れ子のレコード定義
    [GenerateSerializer]
    public record QuestionGroupInfo(Guid GroupId, string Name, List<QuestionReference> Questions);
    
    [GenerateSerializer]
    public record QuestionInfo(
        Guid QuestionId,
        string Text,
        List<QuestionOption> Options,
        bool IsDisplayed,
        List<QuestionResponse> Responses,
        Guid QuestionGroupId,
        string QuestionGroupName // QuestionGroupの名前を含める
    );
    
    // 初期ペイロード生成メソッド
    public static QuestionsMultiProjector GenerateInitialPayload()
        => new(ImmutableDictionary<Guid, QuestionGroupInfo>.Empty, 
               ImmutableDictionary<Guid, QuestionInfo>.Empty);
    
    // マルチプロジェクター名の取得
    public static string GetMultiProjectorName() => nameof(QuestionsMultiProjector);
    
    // プロジェクトメソッドの実装
    public ResultBox<QuestionsMultiProjector> Project(QuestionsMultiProjector payload, IEvent ev) => ev.GetPayload() switch
    {
        // QuestionGroup イベント
        QuestionGroupCreated e => payload with
        {
            QuestionGroups = payload.QuestionGroups.Add(
                ev.PartitionKeys.AggregateId,
                new QuestionGroupInfo(ev.PartitionKeys.AggregateId, e.Name, new List<QuestionReference>()))
        },
        
        QuestionGroupNameUpdated e => UpdateGroupNameAndRelatedQuestions(payload, ev.PartitionKeys.AggregateId, e.Name),
        
        QuestionGroupDeleted => payload with
        {
            QuestionGroups = payload.QuestionGroups.Remove(ev.PartitionKeys.AggregateId)
        },
        
        QuestionAddedToGroup e => AddQuestionToGroup(payload, ev.PartitionKeys.AggregateId, e.QuestionId),
        
        QuestionRemovedFromGroup e => RemoveQuestionFromGroup(payload, ev.PartitionKeys.AggregateId, e.QuestionId),
        
        QuestionOrderChanged e => UpdateQuestionOrder(payload, ev.PartitionKeys.AggregateId, e.QuestionId, e.NewOrder),
        
        // Question イベント
        QuestionCreated e => AddNewQuestion(payload, ev.PartitionKeys.AggregateId, e),
        
        QuestionUpdated e => UpdateExistingQuestion(payload, ev.PartitionKeys.AggregateId, e),
        
        QuestionDeleted => payload with
        {
            Questions = payload.Questions.Remove(ev.PartitionKeys.AggregateId)
        },
        
        QuestionGroupIdUpdated e => UpdateQuestionGroupId(payload, ev.PartitionKeys.AggregateId, e.QuestionGroupId),
        
        QuestionDisplayStarted => UpdateQuestionDisplayStatus(payload, ev.PartitionKeys.AggregateId, true),
        
        QuestionDisplayStopped => UpdateQuestionDisplayStatus(payload, ev.PartitionKeys.AggregateId, false),
        
        ResponseAdded e => AddResponseToQuestion(payload, ev.PartitionKeys.AggregateId, e),
        
        _ => payload // 関係ないイベントは処理しない
    };

    // ヘルパーメソッド: グループ名の更新と関連質問の更新
    private static QuestionsMultiProjector UpdateGroupNameAndRelatedQuestions(
        QuestionsMultiProjector payload, 
        Guid groupId, 
        string newName)
    {
        if (!payload.QuestionGroups.TryGetValue(groupId, out var group))
        {
            return payload; // グループが見つからない場合は変更なし
        }
        
        // グループ名を更新
        var updatedGroups = payload.QuestionGroups.SetItem(
            groupId, 
            group with { Name = newName });
        
        // 関連する質問のグループ名を更新
        var updatedQuestions = payload.Questions;
        foreach (var question in payload.Questions.Values.Where(q => q.QuestionGroupId == groupId))
        {
            updatedQuestions = updatedQuestions.SetItem(
                question.QuestionId,
                question with { QuestionGroupName = newName });
        }
        
        return payload with { 
            QuestionGroups = updatedGroups,
            Questions = updatedQuestions
        };
    }

    // ヘルパーメソッド: 新しい質問の追加
    private static QuestionsMultiProjector AddNewQuestion(
        QuestionsMultiProjector payload,
        Guid questionId,
        QuestionCreated e)
    {
        // グループ名を取得（グループが存在する場合）
        string groupName = "";
        if (payload.QuestionGroups.TryGetValue(e.QuestionGroupId, out var group))
        {
            groupName = group.Name;
        }
        
        // 質問を追加
        var updatedQuestions = payload.Questions.Add(
            questionId,
            new QuestionInfo(
                questionId,
                e.Text,
                e.Options,
                false, // デフォルトでは表示されていない
                new List<QuestionResponse>(),
                e.QuestionGroupId,
                groupName));
        
        return payload with { Questions = updatedQuestions };
    }

    // ヘルパーメソッド: 既存の質問の更新
    private static QuestionsMultiProjector UpdateExistingQuestion(
        QuestionsMultiProjector payload,
        Guid questionId,
        QuestionUpdated e)
    {
        if (!payload.Questions.TryGetValue(questionId, out var question))
        {
            return payload; // 質問が見つからない場合は変更なし
        }
        
        // 質問を更新
        var updatedQuestions = payload.Questions.SetItem(
            questionId,
            question with { 
                Text = e.Text,
                Options = e.Options
            });
        
        return payload with { Questions = updatedQuestions };
    }

    // ヘルパーメソッド: 質問のグループIDの更新
    private static QuestionsMultiProjector UpdateQuestionGroupId(
        QuestionsMultiProjector payload,
        Guid questionId,
        Guid newGroupId)
    {
        if (!payload.Questions.TryGetValue(questionId, out var question))
        {
            return payload; // 質問が見つからない場合は変更なし
        }
        
        // 新しいグループの名前を取得
        string newGroupName = "";
        if (payload.QuestionGroups.TryGetValue(newGroupId, out var group))
        {
            newGroupName = group.Name;
        }
        
        // 質問のグループIDとグループ名を更新
        var updatedQuestions = payload.Questions.SetItem(
            questionId,
            question with { 
                QuestionGroupId = newGroupId,
                QuestionGroupName = newGroupName
            });
        
        return payload with { Questions = updatedQuestions };
    }

    // ヘルパーメソッド: 質問の表示状態の更新
    private static QuestionsMultiProjector UpdateQuestionDisplayStatus(
        QuestionsMultiProjector payload,
        Guid questionId,
        bool isDisplayed)
    {
        if (!payload.Questions.TryGetValue(questionId, out var question))
        {
            return payload; // 質問が見つからない場合は変更なし
        }
        
        // 質問の表示状態を更新
        var updatedQuestions = payload.Questions.SetItem(
            questionId,
            question with { IsDisplayed = isDisplayed });
        
        return payload with { Questions = updatedQuestions };
    }

    // ヘルパーメソッド: 質問への回答の追加
    private static QuestionsMultiProjector AddResponseToQuestion(
        QuestionsMultiProjector payload,
        Guid questionId,
        ResponseAdded e)
    {
        if (!payload.Questions.TryGetValue(questionId, out var question))
        {
            return payload; // 質問が見つからない場合は変更なし
        }
        
        // 新しい回答のリストを作成
        var updatedResponses = new List<QuestionResponse>(question.Responses)
        {
            new QuestionResponse(
                e.ResponseId,
                e.ParticipantName,
                e.SelectedOptionId,
                e.Comment,
                e.Timestamp)
        };
        
        // 質問を更新
        var updatedQuestions = payload.Questions.SetItem(
            questionId,
            question with { Responses = updatedResponses });
        
        return payload with { Questions = updatedQuestions };
    }

    // ヘルパーメソッド: グループへの質問の追加
    private static QuestionsMultiProjector AddQuestionToGroup(
        QuestionsMultiProjector payload,
        Guid groupId,
        Guid questionId)
    {
        if (!payload.QuestionGroups.TryGetValue(groupId, out var group))
        {
            return payload; // グループが見つからない場合は変更なし
        }
        
        // 既に質問が追加されている場合は追加しない
        if (group.Questions.Any(q => q.QuestionId == questionId))
        {
            return payload;
        }
        
        // 次の順序番号を決定
        int nextOrder = group.Questions.Count > 0 ? group.Questions.Max(q => q.Order) + 1 : 0;
        
        // 質問をグループに追加
        var updatedQuestions = new List<QuestionReference>(group.Questions)
        {
            new QuestionReference(questionId, nextOrder)
        };
        
        var updatedGroups = payload.QuestionGroups.SetItem(
            groupId,
            group with { Questions = updatedQuestions });
        
        // 質問側のグループ情報も更新
        var updatedQuestionsDict = payload.Questions;
        if (payload.Questions.TryGetValue(questionId, out var question))
        {
            updatedQuestionsDict = updatedQuestionsDict.SetItem(
                questionId,
                question with { 
                    QuestionGroupId = groupId,
                    QuestionGroupName = group.Name
                });
        }
        
        return payload with { 
            QuestionGroups = updatedGroups,
            Questions = updatedQuestionsDict
        };
    }

    // ヘルパーメソッド: グループからの質問の削除
    private static QuestionsMultiProjector RemoveQuestionFromGroup(
        QuestionsMultiProjector payload,
        Guid groupId,
        Guid questionId)
    {
        if (!payload.QuestionGroups.TryGetValue(groupId, out var group))
        {
            return payload; // グループが見つからない場合は変更なし
        }
        
        // 削除対象の質問を探す
        var questionToRemove = group.Questions.FirstOrDefault(q => q.QuestionId == questionId);
        if (questionToRemove == null)
        {
            return payload; // 質問が見つからない場合は変更なし
        }
        
        // 質問をグループから削除し、順序を再割り当て
        var updatedQuestions = group.Questions
            .Where(q => q.QuestionId != questionId)
            .OrderBy(q => q.Order)
            .Select((q, index) => q with { Order = index })
            .ToList();
        
        var updatedGroups = payload.QuestionGroups.SetItem(
            groupId,
            group with { Questions = updatedQuestions });
        
        return payload with { QuestionGroups = updatedGroups };
    }

    // ヘルパーメソッド: 質問の順序の更新
    private static QuestionsMultiProjector UpdateQuestionOrder(
        QuestionsMultiProjector payload,
        Guid groupId,
        Guid questionId,
        int newOrder)
    {
        if (!payload.QuestionGroups.TryGetValue(groupId, out var group))
        {
            return payload; // グループが見つからない場合は変更なし
        }
        
        // 対象の質問を探す
        var questionToMove = group.Questions.FirstOrDefault(q => q.QuestionId == questionId);
        if (questionToMove == null || newOrder < 0 || newOrder >= group.Questions.Count)
        {
            return payload; // 質問が見つからないか、無効な順序の場合は変更なし
        }
        
        // 現在の順序と新しい順序が同じ場合は変更なし
        if (questionToMove.Order == newOrder)
        {
            return payload;
        }
        
        // 質問のリストから対象の質問を除外
        var otherQuestions = group.Questions.Where(q => q.QuestionId != questionId).ToList();
        
        // 新しい順序に質問を挿入
        otherQuestions.Insert(newOrder, questionToMove);
        
        // 順序を再割り当て
        var updatedQuestions = otherQuestions
            .Select((q, index) => q with { Order = index })
            .ToList();
        
        var updatedGroups = payload.QuestionGroups.SetItem(
            groupId,
            group with { Questions = updatedQuestions });
        
        return payload with { QuestionGroups = updatedGroups };
    }
}
