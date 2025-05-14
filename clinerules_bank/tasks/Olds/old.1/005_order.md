EsCQRSQuestions/EsCQRSQuestions.Domain/Aggregates/Questions/Payloads/Question.cs
を大きく管理する QuestionGroupを作りたい

EsCQRSQuestions/EsCQRSQuestions.Domain/Aggregates
に
QuestionGroups
を作り、
Payloadには
QuetionGroup集約を作ります。Sekibanを利用してください。

ただ、Questionの内容をそのまま中に入れるわけではなく、Questions の AggregateId とその順序をまず管理したい。

また、QuestionGroup自体にも名称をつけたい。

そして、Question を作る前にQuestionGroupを作って、Question側にも、QuestionGroup の AggregateIdを必須で持つようにしたい。

追加として、複数の種類のコマンドを実行するときは、ワークフローを定義してください。


ワークフローは
EsCQRSQuestions/EsCQRSQuestions.Domain/Workflows
配下におき、DIで必要な実行クラス(ISekibanExecutor)をもらい、
コマンドrecordをワークフロークラス内で定義し実行します。
```
public class MyWorkFlow(ISekibanExecutor Executor)
{
    public class Command(int SomeInt, guid SomeGuid)

Task<ResultBox<ReturnValue>> ExecuteAsync(MyWorkflow.Command command)
    =>
    ///workflow code...

}
```


ただ、このタスクでは計画するだけです。
このファイルの下部に計画をよく考えて追記で記入してください。必要なファイルの読み込みなど調査を行い、できるだけ具体的に計画してください。

チャットではなく、このファイル
clinerules_bank/tasks/005_order.md
を編集して、下に現在の設計を書いてください。
+++++++++++以下に計画を書く+++++++++++

# QuestionGroup 実装計画

## 1. ドメインモデル設計

### 1.1. 集約とペイロード定義

#### QuestionGroup 集約 (`EsCQRSQuestions.Domain/Aggregates/QuestionGroups/Payloads/QuestionGroup.cs`)
```csharp
using Sekiban.Pure.Aggregates;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;

[GenerateSerializer]
public record QuestionGroup(
    string Name,
    List<QuestionReference> Questions
) : IAggregatePayload;

[GenerateSerializer]
public record DeletedQuestionGroup(
    string Name,
    List<QuestionReference> Questions
) : IAggregatePayload;
```

#### QuestionReference 値オブジェクト (`EsCQRSQuestions.Domain/ValueObjects/QuestionReference.cs`)
```csharp
namespace EsCQRSQuestions.Domain.ValueObjects;

[GenerateSerializer]
public record QuestionReference(
    Guid QuestionId,
    int Order
);
```

#### Question 集約の更新 (`EsCQRSQuestions.Domain/Aggregates/Questions/Payloads/Question.cs`)
既存のQuestionクラスを拡張して、QuestionGroupIdを追加:
```csharp
[GenerateSerializer]
public record Question(
    string Text,
    List<QuestionOption> Options,
    bool IsDisplayed,
    List<QuestionResponse> Responses,
    Guid QuestionGroupId  // 追加: 所属するQuestionGroupの参照
) : IAggregatePayload;
```

### 1.2. プロジェクター定義

#### QuestionGroupProjector (`EsCQRSQuestions.Domain/Aggregates/QuestionGroups/QuestionGroupProjector.cs`)
```csharp
using Sekiban.Pure.Aggregates;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

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
            (QuestionGroup group, QuestionAddedToGroup e) =>
            {
                var questions = new List<QuestionReference>(group.Questions);
                questions.Add(new QuestionReference(e.QuestionId, e.Order));
                // 順序に基づいて並べ替え
                return group with { Questions = questions.OrderBy(q => q.Order).ToList() };
            },
            
            // 質問の削除
            (QuestionGroup group, QuestionRemovedFromGroup e) =>
            {
                var questions = group.Questions.Where(q => q.QuestionId != e.QuestionId).ToList();
                return group with { Questions = questions };
            },
            
            // 質問の順序変更
            (QuestionGroup group, QuestionOrderChanged e) =>
            {
                var questions = new List<QuestionReference>(group.Questions);
                var index = questions.FindIndex(q => q.QuestionId == e.QuestionId);
                if (index >= 0)
                {
                    questions[index] = questions[index] with { Order = e.NewOrder };
                }
                return group with { Questions = questions.OrderBy(q => q.Order).ToList() };
            },
            
            // グループの削除
            (QuestionGroup group, QuestionGroupDeleted e) =>
                new DeletedQuestionGroup(group.Name, group.Questions),
            
            // その他の場合はペイロードをそのまま返す
            _ => payload
        };
}
```

### 1.3. イベント定義

以下のイベントを `EsCQRSQuestions.Domain/Aggregates/QuestionGroups/Events` ディレクトリに定義:

#### QuestionGroupCreated.cs
```csharp
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionGroupCreated(string Name) : IEventPayload;
```

#### QuestionGroupNameUpdated.cs
```csharp
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionGroupNameUpdated(string Name) : IEventPayload;
```

#### QuestionAddedToGroup.cs
```csharp
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionAddedToGroup(Guid QuestionId, int Order) : IEventPayload;
```

#### QuestionRemovedFromGroup.cs
```csharp
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionRemovedFromGroup(Guid QuestionId) : IEventPayload;
```

#### QuestionOrderChanged.cs
```csharp
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionOrderChanged(Guid QuestionId, int NewOrder) : IEventPayload;
```

#### QuestionGroupDeleted.cs
```csharp
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;

[GenerateSerializer]
public record QuestionGroupDeleted() : IEventPayload;
```

### 1.4. コマンド定義

以下のコマンドを `EsCQRSQuestions.Domain/Aggregates/QuestionGroups/Commands` ディレクトリに定義:

#### CreateQuestionGroup.cs
```csharp
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record CreateQuestionGroup(string Name) : ICommandWithHandler<CreateQuestionGroup, QuestionGroupProjector>
{
    public PartitionKeys SpecifyPartitionKeys(CreateQuestionGroup command) => 
        PartitionKeys.Generate<QuestionGroupProjector>();

    public ResultBox<EventOrNone> Handle(CreateQuestionGroup command, ICommandContext<IAggregatePayload> context)
        => EventOrNone.Event(new QuestionGroupCreated(command.Name));
}
```

#### UpdateQuestionGroupName.cs
```csharp
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record UpdateQuestionGroupName(Guid QuestionGroupId, string Name) : 
    ICommandWithHandler<UpdateQuestionGroupName, QuestionGroupProjector>
{
    public PartitionKeys SpecifyPartitionKeys(UpdateQuestionGroupName command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(UpdateQuestionGroupName command, ICommandContext<IAggregatePayload> context)
        => EventOrNone.Event(new QuestionGroupNameUpdated(command.Name));
}
```

#### AddQuestionToGroup.cs
```csharp
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record AddQuestionToGroup(Guid QuestionGroupId, Guid QuestionId, int Order) : 
    ICommandWithHandler<AddQuestionToGroup, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(AddQuestionToGroup command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(AddQuestionToGroup command, ICommandContext<QuestionGroup> context)
    {
        // 既に追加されている場合はエラー
        var group = context.GetAggregate().Payload;
        if (group.Questions.Any(q => q.QuestionId == command.QuestionId))
        {
            return new ArgumentException($"Question {command.QuestionId} is already in group");
        }
        
        return EventOrNone.Event(new QuestionAddedToGroup(command.QuestionId, command.Order));
    }
}
```

#### RemoveQuestionFromGroup.cs
```csharp
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record RemoveQuestionFromGroup(Guid QuestionGroupId, Guid QuestionId) : 
    ICommandWithHandler<RemoveQuestionFromGroup, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(RemoveQuestionFromGroup command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(RemoveQuestionFromGroup command, ICommandContext<QuestionGroup> context)
    {
        var group = context.GetAggregate().Payload;
        if (!group.Questions.Any(q => q.QuestionId == command.QuestionId))
        {
            return new ArgumentException($"Question {command.QuestionId} is not in group");
        }
        
        return EventOrNone.Event(new QuestionRemovedFromGroup(command.QuestionId));
    }
}
```

#### ChangeQuestionOrder.cs
```csharp
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record ChangeQuestionOrder(Guid QuestionGroupId, Guid QuestionId, int NewOrder) : 
    ICommandWithHandler<ChangeQuestionOrder, QuestionGroupProjector, QuestionGroup>
{
    public PartitionKeys SpecifyPartitionKeys(ChangeQuestionOrder command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(ChangeQuestionOrder command, ICommandContext<QuestionGroup> context)
    {
        var group = context.GetAggregate().Payload;
        if (!group.Questions.Any(q => q.QuestionId == command.QuestionId))
        {
            return new ArgumentException($"Question {command.QuestionId} is not in group");
        }
        
        return EventOrNone.Event(new QuestionOrderChanged(command.QuestionId, command.NewOrder));
    }
}
```

#### DeleteQuestionGroup.cs
```csharp
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;

[GenerateSerializer]
public record DeleteQuestionGroup(Guid QuestionGroupId) : 
    ICommandWithHandler<DeleteQuestionGroup, QuestionGroupProjector>
{
    public PartitionKeys SpecifyPartitionKeys(DeleteQuestionGroup command) => 
        PartitionKeys.Existing<QuestionGroupProjector>(command.QuestionGroupId);

    public ResultBox<EventOrNone> Handle(DeleteQuestionGroup command, ICommandContext<IAggregatePayload> context)
        => EventOrNone.Event(new QuestionGroupDeleted());
}
```

### 1.5. Question 集約の更新コマンド

`EsCQRSQuestions.Domain/Aggregates/Questions/Commands` ディレクトリに以下を追加:

#### CreateQuestion の更新
```csharp
// 既存の CreateQuestion コマンドを更新して QuestionGroupId パラメーターを追加
[GenerateSerializer]
public record CreateQuestion(
    string Text,
    List<QuestionOption> Options,
    bool IsDisplayed,
    List<QuestionResponse> Responses,
    Guid QuestionGroupId
) : ICommandWithHandler<CreateQuestion, QuestionProjector>
{
    // 既存のコード + QuestionGroupId の処理
    // ...
}
```

#### UpdateQuestionGroupId.cs
```csharp
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Documents;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Commands;

[GenerateSerializer]
public record UpdateQuestionGroupId(Guid QuestionId, Guid QuestionGroupId) : 
    ICommandWithHandler<UpdateQuestionGroupId, QuestionProjector>
{
    public PartitionKeys SpecifyPartitionKeys(UpdateQuestionGroupId command) => 
        PartitionKeys.Existing<QuestionProjector>(command.QuestionId);

    public ResultBox<EventOrNone> Handle(UpdateQuestionGroupId command, ICommandContext<IAggregatePayload> context)
        => EventOrNone.Event(new QuestionGroupIdUpdated(command.QuestionGroupId));
}
```

### 1.6. Question 集約のイベント更新

`EsCQRSQuestions.Domain/Aggregates/Questions/Events` ディレクトリに以下を追加:

#### QuestionGroupIdUpdated.cs
```csharp
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.Questions.Events;

[GenerateSerializer]
public record QuestionGroupIdUpdated(Guid QuestionGroupId) : IEventPayload;
```

### 1.7. QuestionProjector の更新

`EsCQRSQuestions.Domain/Aggregates/Questions/QuestionProjector.cs` を更新:

```csharp
// 既存のコードに加えて、QuestionGroupIdUpdated イベントの処理を追加
public IAggregatePayload Project(IAggregatePayload payload, IEvent ev)
    => (payload, ev.GetPayload()) switch
    {
        // 既存のケース
        // ...
        
        // QuestionGroupId の更新
        (Question question, QuestionGroupIdUpdated e) =>
            question with { QuestionGroupId = e.QuestionGroupId },
        
        _ => payload
    };
```

## 2. ワークフローの実装

### 2.1. QuestionGroupWorkflow.cs

`EsCQRSQuestions.Domain/Workflows/QuestionGroupWorkflow.cs` を作成:

```csharp
using Sekiban.Pure.Executors;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Commands;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Workflows;

public class QuestionGroupWorkflow(ISekibanExecutor Executor)
{
    // グループを作成して質問を追加するコマンド
    [GenerateSerializer]
    public record CreateGroupWithQuestionsCommand(
        string GroupName,
        List<(string Text, List<QuestionOption> Options)> Questions
    );

    // 質問をグループ間で移動するコマンド
    [GenerateSerializer]
    public record MoveQuestionBetweenGroupsCommand(
        Guid QuestionId,
        Guid SourceGroupId,
        Guid TargetGroupId,
        int NewOrder
    );

    // グループを作成して質問を追加するワークフロー
    public async Task<ResultBox<Guid>> CreateGroupWithQuestionsAsync(CreateGroupWithQuestionsCommand command)
    {
        // 1. まずグループを作成
        return await Executor.CommandAsync(new CreateQuestionGroup(command.GroupName))
            .Conveyor(async groupResult => {
                var groupId = groupResult.PartitionKeys.AggregateId;
                
                // 2. 各質問を作成してグループに追加
                var questionTasks = new List<Task<ResultBox<bool>>>();
                int order = 0;
                
                foreach (var (text, options) in command.Questions)
                {
                    var task = CreateQuestionAndAddToGroupAsync(text, options, groupId, order++);
                    questionTasks.Add(task);
                }
                
                // すべての質問の作成と追加が完了するのを待つ
                await Task.WhenAll(questionTasks);
                
                // グループIDを返す
                return groupId;
            });
    }
    
    // 質問を作成してグループに追加するヘルパーメソッド
    private async Task<ResultBox<bool>> CreateQuestionAndAddToGroupAsync(
        string text, 
        List<QuestionOption> options, 
        Guid groupId, 
        int order)
    {
        // 1. 質問を作成
        return await Executor.CommandAsync(new CreateQuestion(
                text,
                options,
                false, // 最初は表示しない
                new List<QuestionResponse>(),
                groupId
            ))
            .Conveyor(async questionResult => {
                var questionId = questionResult.PartitionKeys.AggregateId;
                
                // 2. 質問をグループに追加
                await Executor.CommandAsync(new AddQuestionToGroup(
                    groupId, 
                    questionId, 
                    order
                ));
                
                return true;
            });
    }
    
    // 質問をグループ間で移動するワークフロー
    public async Task<ResultBox<bool>> MoveQuestionBetweenGroupsAsync(MoveQuestionBetweenGroupsCommand command)
    {
        // 1. 元のグループから質問を削除
        return await Executor.CommandAsync(new RemoveQuestionFromGroup(
                command.SourceGroupId, 
                command.QuestionId
            ))
            .Conveyor(async _ => {
                // 2. 新しいグループに質問を追加
                await Executor.CommandAsync(new AddQuestionToGroup(
                    command.TargetGroupId,
                    command.QuestionId,
                    command.NewOrder
                ));
                
                // 3. 質問のグループIDを更新
                await Executor.CommandAsync(new UpdateQuestionGroupId(
                    command.QuestionId,
                    command.TargetGroupId
                ));
                
                return true;
            });
    }
}
```

## 3. クエリの実装

### 3.1. クエリクラスの作成

`EsCQRSQuestions.Domain/Aggregates/QuestionGroups/Queries` ディレクトリに以下のファイルを作成:

#### GetQuestionGroupsQuery.cs
```csharp
using Sekiban.Pure.Query;
using Sekiban.Pure.Projectors;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;

[GenerateSerializer]
public record GetQuestionGroupsQuery() : 
    IMultiProjectionListQuery<AggregateListProjector<QuestionGroupProjector>, GetQuestionGroupsQuery, GetQuestionGroupsQuery.ResultRecord>
{
    public static ResultBox<IEnumerable<ResultRecord>> HandleFilter(
        MultiProjectionState<AggregateListProjector<QuestionGroupProjector>> projection, 
        GetQuestionGroupsQuery query, 
        IQueryContext context)
    {
        return projection.Payload.Aggregates
            .Where(m => m.Value.GetPayload() is QuestionGroup)
            .Select(m => ((QuestionGroup)m.Value.GetPayload(), m.Value.PartitionKeys))
            .Select(tuple => new ResultRecord(
                tuple.PartitionKeys.AggregateId, 
                tuple.Item1.Name, 
                tuple.Item1.Questions.Select(q => new QuestionReferenceRecord(q.QuestionId, q.Order)).ToList()))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<ResultRecord>> HandleSort(
        IEnumerable<ResultRecord> filteredList, 
        GetQuestionGroupsQuery query, 
        IQueryContext context)
    {
        // 名前でソート
        return filteredList.OrderBy(m => m.Name).AsEnumerable().ToResultBox();
    }

    [GenerateSerializer]
    public record ResultRecord(
        Guid Id, 
        string Name, 
        List<QuestionReferenceRecord> Questions
    );

    [GenerateSerializer]
    public record QuestionReferenceRecord(
        Guid QuestionId, 
        int Order
    );
}
```

#### GetQuestionsByGroupIdQuery.cs
```csharp
using Sekiban.Pure.Query;
using Sekiban.Pure.Projectors;
using EsCQRSQuestions.Domain.Aggregates.Questions;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;

[GenerateSerializer]
public record GetQuestionsByGroupIdQuery(Guid QuestionGroupId) : 
    IMultiProjectionListQuery<AggregateListProjector<QuestionProjector>, GetQuestionsByGroupIdQuery, GetQuestionsByGroupIdQuery.ResultRecord>
{
    public static ResultBox<IEnumerable<ResultRecord>> HandleFilter(
        MultiProjectionState<AggregateListProjector<QuestionProjector>> projection, 
        GetQuestionsByGroupIdQuery query, 
        IQueryContext context)
    {
        return projection.Payload.Aggregates
            .Where(m => m.Value.GetPayload() is Question q && q.QuestionGroupId == query.QuestionGroupId)
            .Select(m => ((Question)m.Value.GetPayload(), m.Value.PartitionKeys))
            .Select(tuple => new ResultRecord(
                tuple.PartitionKeys.AggregateId, 
                tuple.Item1.Text, 
                tuple.Item1.Options.Select(o => new QuestionOptionRecord(o.Id, o.Text)).ToList(),
                tuple.Item1.IsDisplayed,
                tuple.Item1.QuestionGroupId))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<ResultRecord>> HandleSort(
        IEnumerable<ResultRecord> filteredList, 
        GetQuestionsByGroupIdQuery query, 
        IQueryContext context)
    {
        // このソートは不十分。実際には、GroupのQuestionsリストから取得した順序でソートする必要がある
        // ここでは単純にIDでソート
        return filteredList.OrderBy(m => m.Id).AsEnumerable().ToResultBox();
    }

    [GenerateSerializer]
    public record ResultRecord(
        Guid Id, 
        string Text, 
        List<QuestionOptionRecord> Options,
        bool IsDisplayed,
        Guid QuestionGroupId
    );

    [GenerateSerializer]
    public record QuestionOptionRecord(
        string Id, 
        string Text
    );
}
```

## 4. JSON コンテキストの更新

`EsCQRSQuestionsDomainEventsJsonContext.cs` ファイルに以下の型を追加:

```csharp
// 既存のコードに以下の型を追加
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads.QuestionGroup))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads.DeletedQuestionGroup))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.ValueObjects.QuestionReference))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events.QuestionGroupCreated))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events.QuestionGroupNameUpdated))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events.QuestionAddedToGroup))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events.QuestionRemovedFromGroup))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events.QuestionOrderChanged))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events.QuestionGroupDeleted))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionGroupIdUpdated))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries.GetQuestionGroupsQuery.ResultRecord))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries.GetQuestionGroupsQuery.QuestionReferenceRecord))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries.GetQuestionsByGroupIdQuery.ResultRecord))]
[JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries.GetQuestionsByGroupIdQuery.QuestionOptionRecord))]
```

## 5. API エンドポイントの追加

`EsCQRSQuestions.ApiService/Program.cs` に以下のエンドポイントを追加:

```csharp
// QuestionGroup エンドポイント
apiRoute.MapPost("/questionGroups", 
    async ([FromBody] CreateQuestionGroup command, [FromServices] SekibanOrleansExecutor executor) => 
        await executor.CommandAsync(command).UnwrapBox());

apiRoute.MapPost("/questionGroups/{questionGroupId}/questions", 
    async ([FromBody] AddQuestionToGroup command, [FromServices] SekibanOrleansExecutor executor) => 
        await executor.CommandAsync(command).UnwrapBox());

apiRoute.MapDelete("/questionGroups/{questionGroupId}/questions/{questionId}", 
    async (Guid questionGroupId, Guid questionId, [FromServices] SekibanOrleansExecutor executor) => 
        await executor.CommandAsync(new RemoveQuestionFromGroup(questionGroupId, questionId)).UnwrapBox());

apiRoute.MapPut("/questionGroups/{questionGroupId}/questions/{questionId}/order", 
    async (Guid questionGroupId, Guid questionId, [FromBody] int newOrder, [FromServices] SekibanOrleansExecutor executor) => 
        await executor.CommandAsync(new ChangeQuestionOrder(questionGroupId, questionId, newOrder)).UnwrapBox());

apiRoute.MapGet("/questionGroups", 
    async ([FromServices] SekibanOrleansExecutor executor) => 
    {
        var result = await executor.QueryAsync(new GetQuestionGroupsQuery())
                                  .UnwrapBox();
        return result.Items;
    });

apiRoute.MapGet("/questionGroups/{questionGroupId}/questions", 
    async (Guid questionGroupId, [FromServices] SekibanOrleansExecutor executor) => 
    {
        var result = await executor.QueryAsync(new GetQuestionsByGroupIdQuery(questionGroupId))
                                  .UnwrapBox();
        return result.Items;
    });

// ワークフローエンドポイント
apiRoute.MapPost("/workflows/questionGroups/create", 
    async ([FromBody] QuestionGroupWorkflow.CreateGroupWithQuestionsCommand command, 
           [FromServices] QuestionGroupWorkflow workflow) => 
        await workflow.CreateGroupWithQuestionsAsync(command).UnwrapBox());

apiRoute.MapPost("/workflows/questionGroups/moveQuestion", 
    async ([FromBody] QuestionGroupWorkflow.MoveQuestionBetweenGroupsCommand command, 
           [FromServices] QuestionGroupWorkflow workflow) => 
        await workflow.MoveQuestionBetweenGroupsAsync(command).UnwrapBox());
```

## 6. DI 登録

`EsCQRSQuestions.ApiService/Program.cs` のサービス登録部分に以下を追加:

```csharp
// QuestionGroupWorkflow を DI コンテナに登録
builder.Services.AddScoped<QuestionGroupWorkflow>();
```

## 7. テストの追加

基本的なテストケースを `EsCQRSQuestions.Unit` プロジェクトに追加:

```csharp
using Xunit;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using Sekiban.Pure.xUnit;

namespace EsCQRSQuestions.Unit;

public class QuestionGroupTests : SekibanInMemoryTestBase
{
    protected override SekibanDomainTypes GetDomainTypes() => 
        EsCQRSQuestionsDomainDomainTypes.Generate(EsCQRSQuestionsDomainEventsJsonContext.Default.Options);

    [Fact]
    public void CreateQuestionGroup_ShouldCreateEmptyGroup()
    {
        // Arrange & Act
        var response = GivenCommand(new CreateQuestionGroup("Test Group"));
        
        // Assert
        Assert.Equal(1, response.Version);
        var aggregate = ThenGetAggregate<QuestionGroupProjector>(response.PartitionKeys);
        var group = (QuestionGroup)aggregate.Payload;
        Assert.Equal("Test Group", group.Name);
        Assert.Empty(group.Questions);
    }
    
    [Fact]
    public void AddQuestionToGroup_ShouldAddQuestionWithCorrectOrder()
    {
        // Arrange
        var groupResponse = GivenCommand(new CreateQuestionGroup("Test Group"));
        var groupId = groupResponse.PartitionKeys.AggregateId;
        
        var questionResponse = GivenCommand(new CreateQuestion(
            "Test Question",
            new List<QuestionOption>(),
            false,
            new List<QuestionResponse>(),
            groupId
        ));
        var questionId = questionResponse.PartitionKeys.AggregateId;
        
        // Act
        var addResponse = WhenCommand(new AddQuestionToGroup(groupId, questionId, 1));
        
        // Assert
        Assert.Equal(2, addResponse.Version);
        var aggregate = ThenGetAggregate<QuestionGroupProjector>(groupResponse.PartitionKeys);
        var group = (QuestionGroup)aggregate.Payload;
        Assert.Single(group.Questions);
        Assert.Equal(questionId, group.Questions[0].QuestionId);
        Assert.Equal(1, group.Questions[0].Order);
    }
}
```

## 8. フロントエンド更新

AdminWeb のクライアント更新が必要なため、`EsCQRSQuestions.AdminWeb/QuestionGroupApiClient.cs` を作成:

```csharp
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Workflows;
using System.Net.Http.Json;

namespace EsCQRSQuestions.AdminWeb;

public class QuestionGroupApiClient(HttpClient httpClient)
{
    // グループの一覧を取得
    public async Task<List<GetQuestionGroupsQuery.ResultRecord>> GetQuestionGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<List<GetQuestionGroupsQuery.ResultRecord>>(
            "/api/questionGroups", 
            cancellationToken);
        
        return response ?? new List<GetQuestionGroupsQuery.ResultRecord>();
    }
    
    // グループ内の質問一覧を取得
    public async Task<List<GetQuestionsByGroupIdQuery.ResultRecord>> GetQuestionsInGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<List<GetQuestionsByGroupIdQuery.ResultRecord>>(
            $"/api/questionGroups/{groupId}/questions", 
            cancellationToken);
        
        return response ?? new List<GetQuestionsByGroupIdQuery.ResultRecord>();
    }
    
    // 新しいグループを作成
    public async Task CreateQuestionGroupAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateQuestionGroup(name);
        await httpClient.PostAsJsonAsync("/api/questionGroups", command, cancellationToken);
    }
    
    // グループと質問を一度に作成
    public async Task CreateGroupWithQuestionsAsync(
        string name,
        List<(string Text, List<QuestionOption> Options)> questions,
        CancellationToken cancellationToken = default)
    {
        var command = new QuestionGroupWorkflow.CreateGroupWithQuestionsCommand(name, questions);
        await httpClient.PostAsJsonAsync("/workflows/questionGroups/create", command, cancellationToken);
    }
    
    // 質問の順序を変更
    public async Task ChangeQuestionOrderAsync(
        Guid groupId,
        Guid questionId,
        int newOrder,
        CancellationToken cancellationToken = default)
    {
        await httpClient.PutAsJsonAsync(
            $"/api/questionGroups/{groupId}/questions/{questionId}/order", 
            newOrder, 
            cancellationToken);
    }
    
    // 質問をグループから削除
    public async Task RemoveQuestionFromGroupAsync(
        Guid groupId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        await httpClient.DeleteAsync(
            $"/api/questionGroups/{groupId}/questions/{questionId}", 
            cancellationToken);
    }
    
    // 質問をグループ間で移動
    public async Task MoveQuestionBetweenGroupsAsync(
        Guid questionId,
        Guid sourceGroupId,
        Guid targetGroupId,
        int newOrder,
        CancellationToken cancellationToken = default)
    {
        var command = new QuestionGroupWorkflow.MoveQuestionBetweenGroupsCommand(
            questionId, 
            sourceGroupId, 
            targetGroupId, 
            newOrder);
            
        await httpClient.PostAsJsonAsync(
            "/workflows/questionGroups/moveQuestion", 
            command, 
            cancellationToken);
    }
}
```

最後に、`EsCQRSQuestions.AdminWeb/Program.cs` でクライアントを登録:

```csharp
builder.Services.AddHttpClient<QuestionGroupApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
});
```

## 9. 実装順序と次のステップ

1. ValueObjectsの実装
2. QuestionGroup 集約とプロジェクターの実装
3. イベントクラスの実装
4. コマンドクラスの実装
5. JSON コンテキストの更新
6. Question 集約の更新（QuestionGroupIdの追加）
7. ワークフローの実装
8. クエリの実装
9. APIエンドポイントの追加
10. DI登録の追加
11. ユニットテストの実装
12. フロントエンド実装

これにより、QuestionGroup機能が完全に実装され、Questionの管理が可能になります。