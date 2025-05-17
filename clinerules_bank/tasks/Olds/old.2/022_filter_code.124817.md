# Cline

## 問題分析と解決計画：異なるグループ間で質問表示が混在する問題

ActiveQuestionQueryの実装を詳細に調査し、より実用的な解決策を考案しました。

### 現状の構造と問題点

1. **現在のコード構造**:
   - `Question` クラスには `QuestionGroupId` フィールドがあり、質問がどのグループに属しているかの情報を持っています。
   - `QuestionGroup` クラスには `UniqueCode` フィールドがあり、アクセスコードとして機能しています。
   - `ActiveQuestionQuery` は `IsDisplayed=true` の質問を取得しますが、UniqueCodeによるフィルタリングは行っていません。

2. **問題の詳細**:
   - クライアントがアクティブな質問を取得するときに、**すべての**アクティブな質問が返されてしまいます。
   - UniqueCodeを指定して通知は正しく送られていますが、クライアントがAPI経由で質問を取得する際にフィルタリングされていません。

### 解決策の改善点

既存のドメインモデルを活かした、より効率的な解決策を提案します：

1. **`ActiveQuestionQuery` の修正**:
   ```csharp
   [GenerateSerializer]
   public record ActiveQuestionQuery(string? UniqueCode = null)
       : IMultiProjectionQuery<AggregateListProjector<QuestionProjector>, ActiveQuestionQuery, ActiveQuestionQuery.ActiveQuestionRecord>
   {
       public static ResultBox<ActiveQuestionRecord> HandleQuery(
           MultiProjectionState<AggregateListProjector<QuestionProjector>> projection,
           ActiveQuestionQuery query,
           IQueryContext context)
       {
           // 質問のリストを取得
           var questionsWithGroup = projection.Payload.Aggregates
               .Where(m => m.Value.GetPayload() is Question)
               .Select(m => ((Question)m.Value.GetPayload(), m.Value.PartitionKeys))
               .Where(tuple => tuple.Item1.IsDisplayed)
               .ToList();
               
           // UniqueCodeが指定されている場合、該当するグループIDの質問のみをフィルタリング
           if (!string.IsNullOrEmpty(query.UniqueCode))
           {
               // QuestionGroupのリストを取得するために別のクエリが必要かもしれません
               // この例では、外部から提供されたグループ情報を使用すると仮定します
               
               // この部分は実際の実装では、キャッシュやサービス呼び出しに置き換える必要があります
               // ここでは簡略化のために直接アクセスしていますが、実際は別のコンポーネントから取得する必要があります
               var questionGroups = context.RootPartitionKey != null
                   ? // グループ情報を取得する処理（実際の実装は異なります）
                     // ...
                     new List<(Guid Id, string UniqueCode)>()
                   : new List<(Guid Id, string UniqueCode)>();
                   
               // UniqueCodeに一致するグループIDを探す
               var matchingGroupIds = questionGroups
                   .Where(g => g.UniqueCode == query.UniqueCode)
                   .Select(g => g.Id)
                   .ToList();
                   
               // 該当するグループIDの質問のみをフィルタリング
               questionsWithGroup = questionsWithGroup
                   .Where(q => matchingGroupIds.Contains(q.Item1.QuestionGroupId))
                   .ToList();
           }
           
           var activeQuestion = questionsWithGroup
               .Select(tuple => new ActiveQuestionRecord(
                   tuple.PartitionKeys.AggregateId,
                   tuple.Item1.Text,
                   tuple.Item1.Options,
                   tuple.Item1.Responses.Select(r => new ResponseRecord(
                       r.Id,
                       r.ParticipantName,
                       r.SelectedOptionId,
                       r.Comment,
                       r.Timestamp)).ToList(),
                   tuple.Item1.QuestionGroupId))  // グループIDも返す
               .FirstOrDefault();

           return activeQuestion != null 
               ? activeQuestion.ToResultBox() 
               : new ActiveQuestionRecord(
                   Guid.Empty, 
                   string.Empty, 
                   new List<QuestionOption>(), 
                   new List<ResponseRecord>(),
                   Guid.Empty).ToResultBox();  // デフォルト値にもグループIDを含める
       }

       // レコード定義を更新してQuestionGroupIdを含める
       [GenerateSerializer]
       public record ActiveQuestionRecord(
           Guid QuestionId,
           string Text,
           List<QuestionOption> Options,
           List<ResponseRecord> Responses,
           Guid QuestionGroupId  // 追加：グループID
       );
       
       // 他のレコード定義は変更なし
   }
   ```

2. **プロジェクション間の連携**:
   - ActiveQuestionQueryでUniqueCodeからQuestionGroupIdへのマッピングが必要です。
   - 2つの方法が考えられます：
     1. **新しい複合プロジェクションの作成**: QuestionとQuestionGroupを組み合わせたプロジェクションを作成
     2. **サービス呼び出し**: QueryContextからグループ情報を取得するサービスを呼び出す
   - 実装の簡単さを考慮して、サービス呼び出しをお勧めします。

3. **`QuestionGroupService` の作成**:
   ```csharp
   // 新しいサービスインターフェース
   public interface IQuestionGroupService
   {
       Task<Guid?> GetGroupIdByUniqueCodeAsync(string uniqueCode);
       Task<IEnumerable<(Guid Id, string UniqueCode)>> GetAllGroupsAsync();
   }
   
   // 実装クラス
   public class QuestionGroupService : IQuestionGroupService
   {
       private readonly SekibanOrleansExecutor _executor;
       
       public QuestionGroupService(SekibanOrleansExecutor executor)
       {
           _executor = executor;
       }
       
       public async Task<Guid?> GetGroupIdByUniqueCodeAsync(string uniqueCode)
       {
           var groups = await _executor.QueryAsync(new GetQuestionGroupsQuery()).UnwrapBox();
           var group = groups.Items.FirstOrDefault(g => g.UniqueCode == uniqueCode);
           return group?.Id;
       }
       
       public async Task<IEnumerable<(Guid Id, string UniqueCode)>> GetAllGroupsAsync()
       {
           var groups = await _executor.QueryAsync(new GetQuestionGroupsQuery()).UnwrapBox();
           return groups.Items.Select(g => (g.Id, g.UniqueCode));
       }
   }
   ```

4. **`ActiveQuestionQuery` ハンドラの修正（サービス使用バージョン）**:
   ```csharp
   // Program.csでIQuestionGroupServiceを登録
   builder.Services.AddTransient<IQuestionGroupService, QuestionGroupService>();
   
   // ActiveQuestionQueryの修正：依存関係の注入を活用
   public class ActiveQuestionQueryHandler
   {
       private readonly IQuestionGroupService _groupService;
       
       public ActiveQuestionQueryHandler(IQuestionGroupService groupService)
       {
           _groupService = groupService;
       }
       
       public async Task<ResultBox<ActiveQuestionRecord>> HandleAsync(
           MultiProjectionState<AggregateListProjector<QuestionProjector>> projection,
           ActiveQuestionQuery query)
       {
           // 質問のリストを取得
           var questionsWithGroup = projection.Payload.Aggregates
               .Where(m => m.Value.GetPayload() is Question)
               .Select(m => ((Question)m.Value.GetPayload(), m.Value.PartitionKeys))
               .Where(tuple => tuple.Item1.IsDisplayed)
               .ToList();
               
           // UniqueCodeが指定されている場合、該当するグループIDの質問のみをフィルタリング
           if (!string.IsNullOrEmpty(query.UniqueCode))
           {
               var groupId = await _groupService.GetGroupIdByUniqueCodeAsync(query.UniqueCode);
               if (groupId.HasValue)
               {
                   questionsWithGroup = questionsWithGroup
                       .Where(q => q.Item1.QuestionGroupId == groupId.Value)
                       .ToList();
               }
           }
           
           // 以下は変更なし
       }
   }
   ```

5. **API エンドポイントの修正**:
   ```csharp
   // Program.cs内のエンドポイント定義
   apiRoute.MapGet("/questions/active", async (
       [FromServices] SekibanOrleansExecutor executor,
       [FromQuery] string? uniqueCode) =>
   {
       var activeQuestion = await executor.QueryAsync(new ActiveQuestionQuery(uniqueCode)).UnwrapBox();
       return activeQuestion;
   });
   ```

6. **クライアント側修正の方針**:
   - 前回の設計通り、Questionair.razorとQuestionApiClientを修正して、UniqueCodeを渡すようにします。
   - SignalR通知については現状の実装で問題ありません。

### より実用的な実装方針

現実的な実装を考えると、以下のアプローチが最も効率的です：

1. **複合プロジェクションの作成は避ける**:
   - 複合プロジェクションは設計が複雑になるため、既存のプロジェクションを活用する方が良いでしょう。

2. **StartDisplayCommandにUniqueCodeを追加しない**:
   - Question側にUniqueCodeを保持するのではなく、既存のQuestionGroupIdとQuestionGroupのUniqueCodeのマッピングを利用します。
   - このアプローチにより、既存のドメインモデルの変更を最小限に抑えられます。

3. **APIエンドポイントとクライアント側の変更に集中する**:
   - ActiveQuestionQueryにUniqueCodeパラメータを追加
   - エンドポイントにUniqueCodeパラメータを追加
   - クライアント側でUniqueCodeを渡すように修正

### 必要な変更ファイル

1. **ドメイン関連**：
   - `EsCQRSQuestions.Domain.Aggregates.Questions.Queries.ActiveQuestionQuery` (UniqueCodeパラメータ追加)
   - 必要に応じて `IQuestionGroupService` の追加

2. **APIサービス関連**：
   - `EsCQRSQuestions.ApiService.Program.cs` (エンドポイント修正)

3. **クライアント関連**：
   - `EsCQRSQuestions.Web.QuestionApiClient.cs` (GetActiveQuestionAsync修正)
   - `EsCQRSQuestions.Web.Components.Pages.Questionair.razor` (RefreshActiveQuestion修正)

### 実装計画

1. **ドメインモデルの修正**:
   - ActiveQuestionQueryにUniqueCodeパラメータを追加
   - 必要に応じて、QuestionGroupServiceを作成・登録

2. **API エンドポイントの修正**:
   - /questions/activeエンドポイントを修正してUniqueCodeパラメータに対応

3. **クライアント側の修正**:
   - WebのQuestionApiClient修正
   - Questionair.razorの更新

4. **テスト**:
   - 異なるUniqueCodeを持つグループで質問表示のテスト

この修正により、異なるグループ間で質問表示が混在する問題を解決し、各グループに適切な質問のみが表示されるようになります。
