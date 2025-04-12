using Sekiban.Pure.Executors;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Projections.Questions;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Workflows;

/// <summary>
/// 質問の表示を管理するワークフロー
/// </summary>
public class QuestionDisplayWorkflow
{
    private readonly ISekibanExecutor _executor;

    public QuestionDisplayWorkflow(ISekibanExecutor executor)
    {
        _executor = executor;
    }

    /// <summary>
    /// 質問表示の排他制御を行うワークフロー
    /// グループ内で表示中の質問があれば停止させてから、指定の質問を表示します
    /// </summary>
    public async Task<ResultBox<object>> StartDisplayQuestionExclusivelyAsync(
        Guid questionId)
    {
        // 1. 指定された質問の情報を取得してグループIDを確認
        // QuestionsQueryを使って質問の詳細情報を取得（QuestionGroupIdを含む）
        var questionsResult = await _executor.QueryAsync(new QuestionsQuery(string.Empty));
        
        return await questionsResult.Conveyor(async result => {
            // 対象の質問を見つける
            var questionDetail = result.Items.FirstOrDefault(q => q.QuestionId == questionId);
            if (questionDetail == null)
            {
                return ResultBox.FromException<object>(new Exception($"質問が見つかりません: {questionId}"));
            }
            
            var groupId = questionDetail.QuestionGroupId;
            
            // 2. そのグループ内の質問で表示中のものを全て検索
            var groupQuestions = await _executor.QueryAsync(
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
                    await _executor.CommandAsync(new StopDisplayCommand(displayingQuestion.QuestionId));
                }
                
                // 4. 指定された質問を表示状態にする
                var startResult = await _executor.CommandAsync(new StartDisplayCommand(questionId));
                
                return startResult;
            });
        });
    }
}
