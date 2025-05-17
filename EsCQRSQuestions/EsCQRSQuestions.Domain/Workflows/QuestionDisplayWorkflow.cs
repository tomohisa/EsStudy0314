using Sekiban.Pure.Executors;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Projections.Questions;
using ResultBoxes;
using Sekiban.Pure.Command;
using System.Text.Json;
using Sekiban.Pure.Command.Executor;
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
    public Task<ResultBox<CommandResponseSimple>> StartDisplayQuestionExclusivelyAsync(
        Guid questionId)
    {
        // 1. 指定された質問の情報を取得してグループIDを確認
        // QuestionsQueryを使って質問の詳細情報を取得（QuestionGroupIdを含む）
        return executor.QueryAsync(new QuestionsQuery(string.Empty))
            .Conveyor(result => result.Items.Any(q => q.QuestionId == questionId)
                ? result.Items.First(q => q.QuestionId == questionId).ToResultBox()
                : new Exception($"質問が見つかりません: {questionId}"))
            .Combine(detail => executor.QueryAsync(
                new QuestionsQuery(string.Empty, detail.QuestionGroupId)))
            .Do((detail, questions) => questions.Items.Where(q => q.IsDisplayed && q.QuestionId != questionId).ToList()
                .ToResultBox().ScanEach(async record =>
                {
                    await executor.CommandAsync(new StopDisplayCommand(record.QuestionId));
                }))
            .Conveyor(items => executor.CommandAsync(new StartDisplayCommand(questionId)).ToSimpleCommandResponse());
    }
}
