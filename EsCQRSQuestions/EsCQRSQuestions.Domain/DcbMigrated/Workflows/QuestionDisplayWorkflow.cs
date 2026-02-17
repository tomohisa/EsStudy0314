using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Projections.Questions;
using ResultBoxes;
using Sekiban.Dcb;
using Sekiban.Pure.Command.Executor;

namespace EsCQRSQuestions.Domain.Workflows;

public class QuestionDisplayWorkflow(ISekibanExecutor executor)
{
    public async Task<ResultBox<CommandResponseSimple>> StartDisplayQuestionExclusivelyAsync(Guid questionId)
    {
        return await executor.QueryAsync(new QuestionsQuery(string.Empty))
            .Conveyor(result => result.Items.Any(q => q.QuestionId == questionId)
                ? result.Items.First(q => q.QuestionId == questionId).ToResultBox()
                : new Exception($"質問が見つかりません: {questionId}"))
            .Combine(detail => executor.QueryAsync(new QuestionsQuery(string.Empty, detail.QuestionGroupId)))
            .Do((detail, questions) => questions.Items.Where(q => q.IsDisplayed && q.QuestionId != questionId).ToList()
                .ToResultBox().ScanEach(async record => { await executor.ExecuteAsync(new StopDisplayCommand(record.QuestionId)); }))
            .Conveyor(_ => executor.ExecuteAsync(new StartDisplayCommand(questionId)).ToSimpleCommandResponse());
    }
}
