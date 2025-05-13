using Sekiban.Pure.Executors;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Extensions;
using EsCQRSQuestions.Domain.Projections.Questions;
using ResultBoxes;
using Sekiban.Pure.Command;
using System.Text.Json;

namespace EsCQRSQuestions.Domain.Workflows;

/// <summary>
/// 質問の表示を管理するワークフロー
/// </summary>
public class QuestionDisplayWorkflow(ISekibanExecutor executor)
{
    /// <summary>
    /// 質問表示の排他制御を行うワークフロー
    /// グループ内で表示中の質問があれば停止させてから、指定の質問を表示します
    /// </summary>
    public async Task<ResultBox<CommandResponseSimple>> StartDisplayQuestionExclusivelyAsync(
        Guid questionId)
    {
        // 1. 指定された質問の情報を取得してグループIDを確認
        // QuestionsQueryを使って質問の詳細情報を取得（QuestionGroupIdを含む）
        var questionsResult = await executor.QueryAsync(new QuestionsQuery(string.Empty));
        
        return await questionsResult.Conveyor(async result => {
            // 対象の質問を見つける
            var questionDetail = result.Items.FirstOrDefault(q => q.QuestionId == questionId);
            if (questionDetail == null)
            {
                return ResultBox.FromException<CommandResponseSimple>(new Exception($"質問が見つかりません: {questionId}"));
            }
            
            var groupId = questionDetail.QuestionGroupId;
            
            // 2. そのグループ内の質問で表示中のものを全て検索
            var groupQuestions = await executor.QueryAsync(
                new QuestionsQuery(string.Empty, groupId));
            
            // 処理継続
            return await groupQuestions.Conveyor(async questions => {
                // 3. 表示中の質問があれば停止する
                var displayingQuestions = questions.Items
                    .Where(q => q.IsDisplayed && q.QuestionId != questionId)
                    .ToList();
                
                // 一つずつ停止コマンドを実行
                foreach (var displayingQuestion in displayingQuestions)
                {
                    await executor.CommandAsync(new StopDisplayCommand(displayingQuestion.QuestionId));
                }
                
                // 4. 指定された質問を表示状態にする
                var commandResult = await executor.CommandAsync(new StartDisplayCommand(questionId));
                
                // デバッグ情報
                var type = commandResult.GetType();
                var properties = type.GetProperties().Select(p => p.Name).ToList();
                
                // ResultBox<T>の中身を取得
                var valueProperty = type.GetProperty("Value");
                var valueObj = valueProperty?.GetValue(commandResult);
                
                if (valueObj != null)
                {
                    var valueType = valueObj.GetType();
                    var valueProperties = valueType.GetProperties().Select(p => p.Name).ToList();
                    
                    // AggregateIdを取得
                    var partitionKeysProperty = valueType.GetProperty("PartitionKeys");
                    var partitionKeysObj = partitionKeysProperty?.GetValue(valueObj);
                    
                    if (partitionKeysObj != null)
                    {
                        var partitionKeysType = partitionKeysObj.GetType();
                        var aggregateIdProperty = partitionKeysType.GetProperty("AggregateId");
                        var aggregateId = aggregateIdProperty?.GetValue(partitionKeysObj) as Guid?;
                        
                        if (aggregateId.HasValue)
                        {
                            // LastSortableUniqueIdプロパティを探す
                            var sortableIdProperty = valueType.GetProperty("LastSortableUniqueId") 
                                ?? valueType.GetProperty("SortableUniqueId")
                                ?? valueType.GetProperty("UniqueId");
                            
                            string sortableId = "";
                            if (sortableIdProperty != null)
                            {
                                sortableId = sortableIdProperty.GetValue(valueObj)?.ToString() ?? "";
                            }
                            
                            return ResultBox.FromValue(new CommandResponseSimple(aggregateId.Value, sortableId));
                        }
                    }
                }
                
                // 型情報が取得できなかった場合のフォールバック
                return ResultBox.FromValue(new CommandResponseSimple(questionId, ""));
            });
        });
    }
}
