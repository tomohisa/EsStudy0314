# GitHub Copilot

# IWaitForSortableUniqueIdの実装計画

## 概要

このタスクでは、以下のクエリクラスをIWaitForSortableUniqueIdインターフェースに対応させます：

1. EsCQRSQuestions/EsCQRSQuestions.Domain/Aggregates/ActiveUsers/Queries/ActiveUsersQuery.cs
2. EsCQRSQuestions/EsCQRSQuestions.Domain/Aggregates/QuestionGroups/Queries/GetQuestionGroupByGroupIdQuery.cs
3. EsCQRSQuestions/EsCQRSQuestions.Domain/Aggregates/QuestionGroups/Queries/GetQuestionGroupsQuery.cs
4. EsCQRSQuestions/EsCQRSQuestions.Domain/Aggregates/QuestionGroups/Queries/GetQuestionsByGroupIdQuery.cs

また、EsCQRSQuestions/EsCQRSQuestions.ApiService/Program.csも更新してAPIエンドポイントからこの機能を利用できるようにします。

## IWaitForSortableUniqueIdとは

`IWaitForSortableUniqueId`インターフェースはSekibanフレームワークの機能で、クエリ実行時に特定のイベントが処理されるまで待機するために使用されます。これにより、コマンド実行後にクエリを実行する際に、確実に最新の状態を取得することができます。

このインターフェースを実装するには、以下のプロパティを追加します：
```csharp
string? WaitForSortableUniqueId { get; set; }
```

## 実装計画

### 1. クエリクラスの更新

#### 1.1 ActiveUsersQuery.cs の更新

```csharp
using Sekiban.Pure.Query;

[GenerateSerializer]
public record ActiveUsersQuery(Guid ActiveUsersId)
    : IMultiProjectionQuery<AggregateListProjector<ActiveUsersProjector>, ActiveUsersQuery, ActiveUsersQuery.ActiveUsersRecord>,
      IWaitForSortableUniqueId // インターフェースを追加
{
    // IWaitForSortableUniqueIdの実装
    public string? WaitForSortableUniqueId { get; set; }

    // 既存のHandleQueryメソッドはそのまま
}
```

#### 1.2 GetQuestionGroupByGroupIdQuery.cs の更新

```csharp
using Sekiban.Pure.Query;

[GenerateSerializer]
public record GetQuestionGroupByGroupIdQuery(Guid QuestionGroupId) : 
    IMultiProjectionQuery<AggregateListProjector<QuestionGroupProjector>, GetQuestionGroupByGroupIdQuery, Aggregate<QuestionGroup>>,
    IWaitForSortableUniqueId // インターフェースを追加
{
    // IWaitForSortableUniqueIdの実装
    public string? WaitForSortableUniqueId { get; set; }

    // 既存のHandleQueryメソッドはそのまま
}
```

#### 1.3 GetQuestionGroupsQuery.cs の更新

```csharp
using Sekiban.Pure.Query;

[GenerateSerializer]
public record GetQuestionGroupsQuery() : 
    IMultiProjectionListQuery<AggregateListProjector<QuestionGroupProjector>, GetQuestionGroupsQuery, GetQuestionGroupsQuery.ResultRecord>,
    IWaitForSortableUniqueId // インターフェースを追加
{
    // IWaitForSortableUniqueIdの実装
    public string? WaitForSortableUniqueId { get; set; }

    // 既存のHandleFilterとHandleSortメソッドはそのまま
}
```

#### 1.4 GetQuestionsByGroupIdQuery.cs の更新

```csharp
using Sekiban.Pure.Query;

[GenerateSerializer]
public record GetQuestionsByGroupIdQuery(Guid QuestionGroupId) : 
    IMultiProjectionListQuery<AggregateListProjector<QuestionProjector>, GetQuestionsByGroupIdQuery, GetQuestionsByGroupIdQuery.ResultRecord>,
    IWaitForSortableUniqueId // インターフェースを追加
{
    // IWaitForSortableUniqueIdの実装
    public string? WaitForSortableUniqueId { get; set; }

    // 既存のHandleFilterとHandleSortメソッドはそのまま
}
```

### 2. Program.cs の更新

Program.csファイルでは、関連するAPIエンドポイントを更新して、`waitForSortableUniqueId`クエリパラメータを受け取り、クエリオブジェクトに設定する必要があります。具体的には以下のエンドポイントを更新します：

#### 2.1 ActiveUsersエンドポイントの更新

```csharp
// ActiveUsersエンドポイント
apiRoute
    .MapGet(
        "/activeUsers/{activeUsersId}",
        async (
            Guid activeUsersId,
            [FromQuery] string? waitForSortableUniqueId, // クエリパラメータの追加
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            var query = new ActiveUsersQuery(activeUsersId)
            {
                WaitForSortableUniqueId = waitForSortableUniqueId // プロパティの設定
            };
            return Results.Ok(await executor.QueryAsync(query).UnwrapBox());
        })
    .WithOpenApi()
    .WithName("GetActiveUsers");
```

#### 2.2 QuestionGroupエンドポイントの更新

```csharp
// QuestionGroup by IDエンドポイント
apiRoute
    .MapGet(
        "/questionGroups/{id}",
        async (
            Guid id,
            [FromQuery] string? waitForSortableUniqueId, // クエリパラメータの追加
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            var query = new GetQuestionGroupByGroupIdQuery(id)
            {
                WaitForSortableUniqueId = waitForSortableUniqueId // プロパティの設定
            };
            return Results.Ok(await executor.QueryAsync(query).UnwrapBox());
        })
    .WithOpenApi()
    .WithName("GetQuestionGroupById");

// 全QuestionGroupエンドポイント
apiRoute
    .MapGet(
        "/questionGroups",
        async (
            [FromQuery] string? waitForSortableUniqueId, // クエリパラメータの追加
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            var query = new GetQuestionGroupsQuery()
            {
                WaitForSortableUniqueId = waitForSortableUniqueId // プロパティの設定
            };
            return Results.Ok(await executor.QueryAsync(query).UnwrapBox());
        })
    .WithOpenApi()
    .WithName("GetQuestionGroups");

// グループ内の質問エンドポイント
apiRoute
    .MapGet(
        "/questionGroups/{groupId}/questions",
        async (
            Guid groupId,
            [FromQuery] string? waitForSortableUniqueId, // クエリパラメータの追加
            [FromServices] SekibanOrleansExecutor executor) => 
        {
            var query = new GetQuestionsByGroupIdQuery(groupId)
            {
                WaitForSortableUniqueId = waitForSortableUniqueId // プロパティの設定
            };
            return Results.Ok(await executor.QueryAsync(query).UnwrapBox());
        })
    .WithOpenApi()
    .WithName("GetQuestionsByGroupId");
```

## メリット

この変更により、以下のようなメリットが得られます：

1. **即時一貫性**: コマンド実行後に更新されたデータを確実に取得できます
2. **UIの応答性向上**: ユーザーアクションの結果がすぐに反映されます
3. **実装の簡素化**: 手動でポーリングやリトライロジックを実装する必要がなくなります

## 注意点

1. `WaitForSortableUniqueId`プロパティはnull許容型で定義する必要があります
2. クライアント側では、コマンド実行結果から得られる`LastSortableUniqueId`を次のクエリに渡す必要があります
3. 待機時間が長くなる可能性があるため、必要な場合にのみ使用するべきです

## 次のステップ

この設計が承認されたら、実際の実装を行います。また、クライアント側（Blazorコンポーネント）でも、コマンド実行後のクエリリクエストに`waitForSortableUniqueId`パラメータを渡すように更新することを検討する必要があります。
