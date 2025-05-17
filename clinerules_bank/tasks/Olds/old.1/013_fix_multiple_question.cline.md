# 同時表示防止機能の設計

## 現状の問題
現在、`StartDisplayQuestion` APIを呼び出すと、既に表示中の質問があっても、新しい質問を表示状態にできてしまいます。これにより複数の質問が同時に表示状態になる問題が発生しています。

## 解決策
この問題を解決するため、質問を表示する前に同じグループ内に既に表示状態の質問がないかチェックし、ある場合は先に停止させてから新しい質問を表示するようにするワークフローを実装します。

## 実装方針

### 1. 新しいワークフローの作成

`QuestionDisplayWorkflow`クラスを作成し、以下の機能を実装します：

```csharp
using Sekiban.Pure.Executors;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Projections.Questions;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Workflows;

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
        var questionDetail = await _executor.QueryAsync(new QuestionDetailQuery(questionId));
        
        return await questionDetail.Conveyor(async detail => {
            var groupId = detail.QuestionGroupId;
            
            // 2. そのグループ内の質問で表示中のものを全て検索
            var groupQuestions = await _executor.QueryAsync(
                new QuestionsQuery(string.Empty, groupId));
            
            // 処理継続
            return await groupQuestions.Conveyor(async questions => {
                // 3. 表示中の質問があれば停止する
                var displayingQuestions = questions.Items
                    .Where(q => q.IsDisplaying && q.Id != questionId)
                    .ToList();
                
                // 一つずつ停止コマンドを実行
                foreach (var displayingQuestion in displayingQuestions)
                {
                    await _executor.CommandAsync(new StopDisplayCommand(displayingQuestion.Id));
                }
                
                // 4. 指定された質問を表示状態にする
                var startResult = await _executor.CommandAsync(new StartDisplayCommand(questionId));
                
                return startResult;
            });
        });
    }
}
```

### 2. Program.csの修正

APIエンドポイントで新しいワークフローを使用するように変更します：

```csharp
apiRoute
    .MapPost(
        "/questions/startDisplay",
        async (
            [FromBody] StartDisplayCommand command,
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            // ワークフローを使って排他制御を実装
            var workflow = new QuestionDisplayWorkflow(executor);
            return await workflow.StartDisplayQuestionExclusivelyAsync(command.QuestionId).UnwrapBox();
        })
    .WithOpenApi()
    .WithName("StartDisplayQuestion");
```

## 実装の流れ
1. `QuestionDisplayWorkflow.cs`を作成し、上記のコードを実装
2. `Program.cs`の`/questions/startDisplay`エンドポイントを修正して新しいワークフローを使用するように変更
3. テスト: 
   - 同じグループ内で複数の質問を連続して表示させ、一つだけが表示状態になることを確認
   - 異なるグループの質問を表示させても互いに干渉しないことを確認

## 期待される動作
- あるグループ内の質問Aが表示中に質問Bの表示を開始すると：
  1. 質問Aの表示が自動的に停止
  2. 質問Bの表示が開始
  3. グループ内で表示状態の質問は常に最大1つに制限される
- 異なるグループの質問は互いに影響せず、それぞれのグループで1つずつ表示できる

この実装により、質問の表示状態が排他的に制御され、ユーザー体験が改善されます。
