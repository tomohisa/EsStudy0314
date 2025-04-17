# 設計計画: QuestionGroup への UniqueCode 追加

## 1. 目的

`QuestionGroup` アグリゲートに、システム全体でユニークな6桁の英数字コード (`UniqueCode`) を追加する。このコードはグループ作成時に自動生成されるか、ユーザーが指定できるものとし、既存のアクティブなグループと重複しないことを保証する。

## 2. 設計方針

- **Domain層:** `UniqueCode` を保持するためのプロパティ、イベント、コマンド、クエリ、および重複チェックロジックを含むワークフローを追加する。
- **ApiService層:** グループ作成APIエンドポイントを修正し、`UniqueCode` の処理と重複チェックワークフローの呼び出しを組み込む。
- **AdminWeb層:** 管理画面UIを更新し、`UniqueCode` の表示と、作成時の任意指定を可能にする。

## 3. 実装詳細

### 3.1. Domain層 (`EsCQRSQuestions.Domain`)

#### 3.1.1. ペイロード (`Aggregates/QuestionGroups/Payloads/QuestionGroup.cs`)

- `QuestionGroup` レコードに `UniqueCode` プロパティを追加する。
  ```csharp
  [GenerateSerializer, Immutable]
  public record QuestionGroup(
      string Name,
      string UniqueCode, // 追加
      List<QuestionReference> Questions) : IAggregatePayload
  {
      // コンストラクタも修正
      public QuestionGroup() : this("", "", new List<QuestionReference>()) { } 
      // ... 他のメソッド ...
  }
  ```

#### 3.1.2. イベント (`Aggregates/QuestionGroups/Events/QuestionGroupCreated.cs`)

- `QuestionGroupCreated` イベントに `UniqueCode` プロパティを追加する。
  ```csharp
  [GenerateSerializer, Immutable]
  public record QuestionGroupCreated(
      Guid QuestionGroupId, 
      string Name, 
      string UniqueCode, // 追加
      List<Guid> InitialQuestionIds) : IEventPayload; // 必要に応じて InitialQuestionIds も調整
  ```
  *Note:* 既存の `CreateQuestionGroup` コマンドを見ると `InitialQuestionIds` は使われていないようなので、削除または調整が必要かもしれませぬ。ここでは要求に従い `UniqueCode` のみ追加しまする。

#### 3.1.3. プロジェクター (`Aggregates/QuestionGroups/QuestionGroupProjector.cs`)

- `QuestionGroupProjector` の `Project` メソッドを修正し、`QuestionGroupCreated` イベントを処理してペイロードに `UniqueCode` を設定するようにする。
  ```csharp
  public class QuestionGroupProjector : IAggregateProjector
  {
      public IAggregatePayload Project(IAggregatePayload payload, IEvent ev)
          => (payload, ev.GetPayload()) switch
          {
              (EmptyAggregatePayload, QuestionGroupCreated e) => 
                  new QuestionGroup(e.Name, e.UniqueCode, new List<QuestionReference>()), // UniqueCode を設定
              // ... 他のイベントハンドリング ...
              (QuestionGroup group, QuestionGroupCreated e) => // 既に存在するケースも考慮 (通常は発生しないはずだが念のため)
                  group with { Name = e.Name, UniqueCode = e.UniqueCode }, 
              (QuestionGroup group, QuestionGroupNameUpdated e) => 
                  group with { Name = e.NewName },
              // ... AddQuestionToGroup, RemoveQuestionFromGroup, ChangeQuestionOrder の処理 ...
              _ => payload
          };
  }
  ```
  *Note:* `AddQuestionToGroup`, `RemoveQuestionFromGroup`, `ChangeQuestionOrder` に対応するイベントと、それらを処理する case も必要でござる。

#### 3.1.4. コマンド (`Aggregates/QuestionGroups/Commands/CreateQuestionGroup.cs`)

- `CreateQuestionGroup` コマンドにオプショナルな `UniqueCode` パラメータを追加する。
  ```csharp
  [GenerateSerializer]
  public record CreateQuestionGroup(string Name, string? UniqueCode = null) // UniqueCode を追加 (nullable)
      : ICommandWithHandler<CreateQuestionGroup, QuestionGroupProjector>
  {
      public PartitionKeys SpecifyPartitionKeys(CreateQuestionGroup command) => 
          PartitionKeys.Generate<QuestionGroupProjector>();

      // ハンドラはワークフローに移動するため、ここでは削除または単純化する
      // public ResultBox<EventOrNone> Handle(...) => ... 
      // ワークフローから呼び出される際に UniqueCode が確定している前提でイベントを生成する Handle は必要
       public ResultBox<EventOrNone> Handle(CreateQuestionGroup command, ICommandContext<IAggregatePayload> context)
          => context.GetAggregate()
              .Conveyor(aggregate => {
                  var groupId = aggregate.PartitionKeys.AggregateId;
                  // command.UniqueCode はワークフローで設定された確定値が入る想定
                  var uniqueCodeToUse = command.UniqueCode ?? throw new InvalidOperationException("UniqueCode must be set by workflow."); 
                  // InitialQuestionIds は現状使われていないため空リストを渡す
                  return EventOrNone.Event(new QuestionGroupCreated(groupId, command.Name, uniqueCodeToUse, new List<Guid>())); 
              });
  }
  ```
  *Note:* コマンドハンドラロジックは重複チェックを含むワークフローに移行するため、このコマンド自体には `Handle` メソッドは不要になるか、非常に単純化されまする。Sekiban の規約上 `ICommandWithHandler` を実装する場合は `Handle` が必要ですが、ワークフロー経由で実行する場合は不要になる可能性があり申す。ここではワークフローから呼び出される前提の `Handle` を実装しまする。

#### 3.1.5. クエリ (`Aggregates/QuestionGroups/Queries/QuestionGroupUniqueCodeExistsQuery.cs`) - 新規作成

- 指定された `UniqueCode` が存在するかどうかを確認する Non-List Query を作成する。
  ```csharp
  using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
  using EsCQRSQuestions.Domain.Projections; // AggregateListProjector を含む名前空間
  using ResultBoxes;
  using Sekiban.Pure.Aggregates;
  using Sekiban.Pure.Query;
  using Sekiban.Pure.Query.QueryModel;
  using System.Linq; // Linq を使うため

  namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;

  [GenerateSerializer]
  public record QuestionGroupUniqueCodeExistsQuery(string UniqueCode)
      : IMultiProjectionQuery<AggregateListProjector<QuestionGroupProjector>, QuestionGroupUniqueCodeExistsQuery, bool>
  {
      public static ResultBox<bool> HandleQuery(
          MultiProjectionState<AggregateListProjector<QuestionGroupProjector>> projection,
          QuestionGroupUniqueCodeExistsQuery query,
          IQueryContext context)
      {
          bool exists = projection.Payload.Aggregates.Values
              .Select(agg => agg.GetPayload())
              .OfType<QuestionGroup>() // QuestionGroup 型のペイロードのみを対象
              .Any(group => group.UniqueCode != null && group.UniqueCode.Equals(query.UniqueCode, StringComparison.OrdinalIgnoreCase)); // 大文字小文字を区別せずに比較 (null チェック追加)

          return ResultBox.FromValue(exists);
      }
  }
  ```

#### 3.1.6. ワークフロー (`Workflows/QuestionGroupWorkflow.cs`)

- `UniqueCode` 生成ヘルパーメソッドを追加する。
- 重複チェックと `CreateQuestionGroup` コマンド実行を行う新しいワークフローメソッド `CreateQuestionGroupWithUniqueCodeAsync` を追加する。

  ```csharp
  using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands;
  using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries; // 追加
  using ResultBoxes;
  using Sekiban.Pure.Command; // CommandResponse を使うため
  using Sekiban.Pure.Executors;
  using System; // ArgumentException, ApplicationException を使うため
  using System.Security.Cryptography; // RandomNumberGenerator を使うため
  using System.Text; // StringBuilder を使うため
  using System.Threading.Tasks; // Task を使うため

  namespace EsCQRSQuestions.Domain.Workflows;

  public class QuestionGroupWorkflow
  {
      private readonly ISekibanExecutor _executor;
      private const int UniqueCodeLength = 6;
      private const string AllowedChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789"; // 紛らわしい I, l, O を除外
      private const int MaxRetryAttempts = 5; // 重複時のリトライ回数上限

      public QuestionGroupWorkflow(ISekibanExecutor executor)
      {
          _executor = executor;
      }

      // ... 既存のメソッド ...

      /// <summary>
      /// Creates a question group ensuring the UniqueCode is unique.
      /// If UniqueCode is not provided, generates a random one.
      /// </summary>
      public async Task<ResultBox<CommandResponse>> CreateQuestionGroupWithUniqueCodeAsync(CreateQuestionGroup command)
      {
          string targetUniqueCode = command.UniqueCode ?? GenerateRandomUniqueCode();
          int attempt = 0;

          while (attempt <= MaxRetryAttempts)
          {
              // 1. Check for duplicates
              var existsResult = await _executor.QueryAsync(new QuestionGroupUniqueCodeExistsQuery(targetUniqueCode));
              
              // クエリが成功したか確認し、失敗ならエラーを返す
              if (!existsResult.IsSuccess)
              {
                  return ResultBox.FromException<CommandResponse>(
                      existsResult.GetException() ?? new ApplicationException("Failed to check unique code existence."));
              }
              bool codeExists = existsResult.GetValue(); 

              if (!codeExists)
              {
                  // 2. If unique, execute the command with the final code
                  // CreateQuestionGroup の Handle でイベントが生成される
                  var finalCommand = command with { UniqueCode = targetUniqueCode }; 
                  return await _executor.CommandAsync(finalCommand); 
              }

              // 3. If duplicate and code was user-provided, return error immediately
              if (command.UniqueCode != null && attempt == 0) // ユーザー指定コードが初回で重複
              {
                  return ResultBox.FromException<CommandResponse>(
                      new ArgumentException($"UniqueCode '{command.UniqueCode}' already exists."));
              }
              
              // 4. If duplicate and code was generated, generate a new one and retry
              if (command.UniqueCode == null) 
              {
                  targetUniqueCode = GenerateRandomUniqueCode();
                  attempt++;
              }
              else // ユーザー指定コードが重複した場合 (通常は初回でエラーになるはずだが念のため)
              {
                   return ResultBox.FromException<CommandResponse>(
                      new ArgumentException($"UniqueCode '{command.UniqueCode}' already exists."));
              }
          }

          // Max retries exceeded for generated code
          return ResultBox.FromException<CommandResponse>(
              new ApplicationException($"Failed to generate a unique code after {MaxRetryAttempts} attempts."));
      }

      /// <summary>
      /// Generates a random alphanumeric string of the specified length.
      /// </summary>
      private string GenerateRandomUniqueCode()
      {
          var result = new StringBuilder(UniqueCodeLength);
          var allowedCharCount = AllowedChars.Length;
          
          for (int i = 0; i < UniqueCodeLength; i++)
          {
              // Use cryptographically secure random number generator
              var randomIndex = RandomNumberGenerator.GetInt32(allowedCharCount);
              result.Append(AllowedChars[randomIndex]);
          }
          return result.ToString();
      }
  }
  ```

#### 3.1.7. JSON Context (`EsCQRSQuestionsDomainEventsJsonContext.cs`)
- 新しいイベント `QuestionGroupCreated` (修正後) と関連ペイロードがシリアライズ対象に含まれていることを確認する。既存の `QuestionGroupCreated` があれば、その定義が更新されるため、通常は追加の変更は不要でござる。

### 3.2. ApiService層 (`EsCQRSQuestions.ApiService/Program.cs`)

- `/questionGroups` POST エンドポイントを修正する。
  ```csharp
  using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands; // CreateQuestionGroup を使うため
  using EsCQRSQuestions.Domain.Workflows; // QuestionGroupWorkflow を使うため
  using Microsoft.AspNetCore.Mvc; // ProblemDetails, Results を使うため

  // ... 他の using ...

  // Commands
  apiRoute
      .MapPost(
          "/questionGroups",
          async (
              [FromBody] CreateQuestionGroup command, // ボディから Name と Optional な UniqueCode を受け取る
              [FromServices] SekibanOrleansExecutor executor) => 
          {
              // ワークフローをインスタンス化して呼び出す
              var workflow = new QuestionGroupWorkflow(executor);
              var result = await workflow.CreateQuestionGroupWithUniqueCodeAsync(command);
              
              // ResultBox の結果を処理
              return result.Match(
                  successResponse => Results.Ok(successResponse), // 成功時は CommandResponse を返す
                  error => 
                  {
                      // エラーの種類に応じて適切なステータスコードを返す
                      if (error is ArgumentException) 
                      {
                          // 409 Conflict for duplicate
                          return Results.Conflict(new ProblemDetails { Title = "Duplicate Unique Code", Detail = error.Message, Status = StatusCodes.Status409Conflict }); 
                      }
                      if (error is ApplicationException)
                      {
                           // 500 for retry failure or other application errors
                           return Results.Problem(detail: error.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Failed to Create Group"); 
                      }
                      // その他の予期せぬエラー
                      return Results.Problem(detail: error.Message, statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred"); 
                  });
          })
      .WithOpenApi()
      .WithName("CreateQuestionGroup"); // 名前は既存のまま or 変更？ -> そのままで良いか
  ```

### 3.3. AdminWeb層 (`EsCQRSQuestions.AdminWeb`)

#### 3.3.1. クエリ結果 (`Domain/Aggregates/QuestionGroups/Queries/GetQuestionGroupsQuery.cs`)
- `GetQuestionGroupsQuery.ResultRecord` に `UniqueCode` を追加する。
  ```csharp
   // GetQuestionGroupsQuery.cs 内
   using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads; // QuestionReference を使うため
   using System; // Guid, DateTime を使うため
   using System.Collections.Generic; // List を使うため

   namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
   
   // ... Query 定義 ...

   [GenerateSerializer]
   public record ResultRecord(
       Guid Id, 
       string Name, 
       string UniqueCode, // 追加
       List<QuestionReference> Questions, 
       int Version, 
       DateTime LastModified);
  ```
- `GetQuestionGroupsQuery` の `HandleFilter` または `HandleSort` (もしあれば) を修正し、ペイロードから `UniqueCode` を取得して `ResultRecord` に含めるようにする。
  ```csharp
  // GetQuestionGroupsQuery.cs 内の HandleFilter (例)
  public static ResultBox<IEnumerable<ResultRecord>> HandleFilter(
      MultiProjectionState<AggregateListProjector<QuestionGroupProjector>> projection, 
      GetQuestionGroupsQuery query, 
      IQueryContext context)
  {
      return projection.Payload.Aggregates
          .Where(m => m.Value.GetPayload() is QuestionGroup)
          .Select(m => {
              var payload = (QuestionGroup)m.Value.GetPayload();
              var keys = m.Value.PartitionKeys;
              return new ResultRecord(
                  keys.AggregateId, 
                  payload.Name, 
                  payload.UniqueCode, // UniqueCode を含める
                  payload.Questions, 
                  m.Value.Version, 
                  m.Value.LastModified);
          })
          .ToResultBox();
  }
  ```

#### 3.3.2. UI表示 (`Components/Pages/Planning.razor`, `Components/Planning/QuestionGroupList.razor`)
- `QuestionGroupList.razor` (またはグループを表示している箇所) を修正し、グループ名の横などに `UniqueCode` を表示する。
  ```html
  <!-- QuestionGroupList.razor (例) -->
  @* ... using statements ... *@
  @using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries

  @if (Groups != null)
  {
      <ul class="list-group">
          @foreach (var group in Groups)
          {
              <li class="list-group-item list-group-item-action @(group.Id == SelectedGroupId ? "active" : "")" 
                  style="cursor: pointer;"
                  @onclick="() => OnGroupSelected.InvokeAsync(group.Id)">
                  <div class="d-flex w-100 justify-content-between">
                      <h5 class="mb-1">@group.Name</h5>
                      <small class="text-muted">Code: @group.UniqueCode</small> @* UniqueCode を表示 *@
                  </div>
                  <small class="text-muted">@group.Questions.Count question(s)</small>
              </li>
          }
      </ul>
      <button class="btn btn-primary mt-3" @onclick="OnCreateGroupClicked">Create New Group</button>
  }
  else
  {
      <p>Loading groups...</p>
  }

  @code {
      [Parameter] public List<GetQuestionGroupsQuery.ResultRecord>? Groups { get; set; }
      [Parameter] public Guid? SelectedGroupId { get; set; }
      [Parameter] public EventCallback<Guid> OnGroupSelected { get; set; }
      [Parameter] public EventCallback OnCreateGroupClicked { get; set; }
  }
  ```
- `Planning.razor` の `@code` ブロックで `groups` を取得する際に `UniqueCode` が含まれるようにする (上記 3.3.1 の修正が必要)。

#### 3.3.3. グループ作成/編集モーダル (`Components/Planning/GroupFormModal.razor`, `Models/GroupEditModel.cs`)
- `GroupEditModel.cs` に `UniqueCode` プロパティ (nullable string) を追加する。
  ```csharp
  using System.ComponentModel.DataAnnotations;

  namespace EsCQRSQuestions.AdminWeb.Models;

  public class GroupEditModel
  {
      [Required]
      public string Name { get; set; } = string.Empty;

      [RegularExpression(@"^[a-zA-Z0-9]{6}$", ErrorMessage = "Unique Code must be 6 alphanumeric characters.")]
      public string? UniqueCode { get; set; } // 追加 (Nullable), Validation追加
  }
  ```
- `GroupFormModal.razor` に `UniqueCode` の入力フィールドを追加する。
  - 新規作成モード (`IsEditMode == false`) では、入力可能とし、プレースホルダーで「空欄で自動生成」を示す。
  - 編集モード (`IsEditMode == true`) では、読み取り専用 (`readonly`) で表示する。
  ```html
  <!-- GroupFormModal.razor (例) -->
  @using EsCQRSQuestions.AdminWeb.Models
  @using Microsoft.AspNetCore.Components.Forms

  <div class="modal fade" id="groupModal" tabindex="-1" aria-labelledby="groupModalLabel" aria-hidden="true">
      <div class="modal-dialog">
          <div class="modal-content">
              <EditForm Model="Model" OnValidSubmit="HandleSave">
                  <DataAnnotationsValidator />
                  <div class="modal-header">
                      <h5 class="modal-title" id="groupModalLabel">@(IsEditMode ? "Edit" : "Create") Group</h5>
                      <button type="button" class="btn-close" @onclick="HandleClose" aria-label="Close"></button>
                  </div>
                  <div class="modal-body">
                      <div class="mb-3">
                          <label for="groupName" class="form-label">Group Name</label>
                          <InputText id="groupName" class="form-control" @bind-Value="Model.Name" />
                          <ValidationMessage For="@(() => Model.Name)" />
                      </div>

                      <div class="mb-3">
                          <label for="groupUniqueCode" class="form-label">Unique Code</label>
                          @if (IsEditMode)
                          {
                              <InputText id="groupUniqueCode" class="form-control" @bind-Value="Model.UniqueCode" readonly />
                              <div class="form-text">Unique code cannot be changed after creation.</div>
                          }
                          else
                          {
                              <InputText id="groupUniqueCode" class="form-control" @bind-Value="Model.UniqueCode" placeholder="Leave blank to auto-generate (6 chars)" />
                              <ValidationMessage For="@(() => Model.UniqueCode)" /> 
                              <div class="form-text">Enter 6 alphanumeric characters or leave blank.</div>
                          }
                      </div>
                  </div>
                  <div class="modal-footer">
                      <button type="button" class="btn btn-secondary" @onclick="HandleClose">Cancel</button>
                      <button type="submit" class="btn btn-primary">Save</button>
                  </div>
              </EditForm>
          </div>
      </div>
  </div>

  @code {
      [Parameter] public bool IsEditMode { get; set; }
      [Parameter] public GroupEditModel Model { get; set; } = new();
      [Parameter] public EventCallback<GroupEditModel> OnSave { get; set; }
      [Parameter] public EventCallback OnClose { get; set; }

      private async Task HandleSave()
      {
          await OnSave.InvokeAsync(Model);
      }

      private async Task HandleClose()
      {
          await OnClose.InvokeAsync();
      }
  }
  ```
- `Planning.razor` の `SaveGroup` メソッドを修正し、`QuestionGroupApiClient.CreateGroupAsync` を呼び出す際に `groupModel.UniqueCode` を渡すようにする。
  ```csharp
  // Planning.razor @code 内
  private async Task SaveGroup()
  {
      try
      {
          Console.WriteLine("Saving group...");
          if (isEditGroupMode && editGroupId.HasValue)
          {
              // Update (UniqueCode は変更しない)
              await QuestionGroupApi.UpdateGroupAsync(editGroupId.Value, groupModel.Name);
              Console.WriteLine("Group updated");
          }
          else
          {
              // Create (UniqueCode を渡す)
              await QuestionGroupApi.CreateGroupAsync(groupModel.Name, groupModel.UniqueCode); 
              Console.WriteLine("Group created");
          }

          await CloseGroupModal();
          await RefreshGroups(); // グループリストを更新して新しいコードを表示
      }
      catch (Exception ex)
      {
          Console.Error.WriteLine($"Error saving group: {ex.Message}");
          // TODO: Display error to user (e.g., using a toast notification)
          await JsRuntime.InvokeVoidAsync("alert", $"Error saving group: {ex.Message}"); // Simple alert for now
      }
  }
  
  // OpenEditGroupModal も修正して UniqueCode をモデルにセットする
  private async Task OpenEditGroupModal(Guid groupId)
  {
      isEditGroupMode = true;
      editGroupId = groupId;

      var group = groups?.FirstOrDefault(g => g.Id == groupId);
      if (group is not null)
      {
          groupModel = new GroupEditModel
          {
              Name = group.Name,
              UniqueCode = group.UniqueCode // UniqueCode をモデルにセット
          };
          await ShowGroupModal();
      }
  }
  ```

#### 3.3.4. APIクライアント (`QuestionGroupApiClient.cs`)
- `CreateGroupAsync` メソッドを修正し、オプショナルな `uniqueCode` パラメータを受け取り、APIリクエストに含めるようにする。
  ```csharp
  using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Commands; // CreateQuestionGroup を使うため
  using System.Net.Http.Json; // PostAsJsonAsync, ReadFromJsonAsync を使うため
  using System.Net; // HttpStatusCode を使うため
  using Microsoft.AspNetCore.Mvc; // ProblemDetails を使うため (APIが返す場合)

  namespace EsCQRSQuestions.AdminWeb;

  public class QuestionGroupApiClient
  {
      private readonly HttpClient _httpClient;

      public QuestionGroupApiClient(HttpClient httpClient)
      {
          _httpClient = httpClient;
      }
      
      // ... 他のメソッド (GetGroupsAsync, UpdateGroupAsync etc.) ...

      public async Task CreateGroupAsync(string name, string? uniqueCode = null) // uniqueCode パラメータ追加
      {
          // UniqueCode が空文字列の場合は null に変換 (API側で null を期待するため)
          var codeToSend = string.IsNullOrWhiteSpace(uniqueCode) ? null : uniqueCode;
          var command = new CreateQuestionGroup(name, codeToSend); // コマンドオブジェクトを作成
          
          var response = await _httpClient.PostAsJsonAsync("/api/questionGroups", command); // コマンドを送信

          if (!response.IsSuccessStatusCode)
          {
              string errorMessage = response.ReasonPhrase ?? "Unknown error";
              // エラーレスポンスの内容を読み取る試み
              try 
              {
                  var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                  if (!string.IsNullOrWhiteSpace(problemDetails?.Detail))
                  {
                      errorMessage = problemDetails.Detail;
                  } else if (!string.IsNullOrWhiteSpace(problemDetails?.Title)) {
                      errorMessage = problemDetails.Title;
                  }
              } 
              catch 
              {
                  // JSONデシリアライズ失敗時は元のReasonPhraseを使用
              }

              // エラーハンドリング (Conflict など)
              if (response.StatusCode == HttpStatusCode.Conflict) 
              {
                  throw new ApplicationException($"Failed to create group (Conflict): {errorMessage}");
              }
              // その他のエラー
              throw new ApplicationException($"Failed to create group ({(int)response.StatusCode}): {errorMessage}");
          }
      }
      
      // UpdateGroupAsync は UniqueCode を含めないようにする
      public async Task UpdateGroupAsync(Guid groupId, string name)
      {
          // UpdateQuestionGroupName コマンドを使用 (UniqueCode を含まない)
          var command = new UpdateQuestionGroupName(groupId, name); 
          var response = await _httpClient.PutAsJsonAsync($"/api/questionGroups/{groupId}", command);
          response.EnsureSuccessStatusCode();
      }
      
      // GetGroupsAsync は ResultRecord が UniqueCode を含むように修正されている前提
      public async Task<List<GetQuestionGroupsQuery.ResultRecord>?> GetGroupsAsync()
      {
          return await _httpClient.GetFromJsonAsync<List<GetQuestionGroupsQuery.ResultRecord>>("/api/questionGroups");
      }

      // ... 他のメソッド ...
  }
  ```
  *Note:* `CreateQuestionGroup` レコードが API クライアント側でも利用可能である必要があり申す。最も簡単なのは Domain プロジェクトへの参照を追加することですが、共有ライブラリや DTO を使う方法もあり申す。ここでは Domain 参照があると仮定しまする。`UpdateQuestionGroupName` コマンドも参照可能である必要があり申す。

## 4. テスト

- **Unit Tests (`EsCQRSQuestions.Unit`):**
    - `QuestionGroupProjector` が `QuestionGroupCreated` イベントで `UniqueCode` を正しく設定することを検証するテスト。
    - `QuestionGroupWorkflow` の `GenerateRandomUniqueCode` が正しい形式のコードを生成することを検証するテスト。
    - `QuestionGroupWorkflow` の `CreateQuestionGroupWithUniqueCodeAsync` が重複チェック、リトライ、コマンド実行を正しく行うことを検証するテスト (InMemorySekibanExecutor を使用)。
    - `QuestionGroupUniqueCodeExistsQuery` が正しく重複を検出することを検証するテスト。
- **Integration Tests:**
    - API エンドポイント `/questionGroups` (POST) が、`UniqueCode` 指定あり/なしの場合、および重複する場合に正しく動作することを検証するテスト。

## 5. 考慮事項

- **UniqueCode のフォーマット:** 6桁英数字 (大文字小文字区別なし) とする。紛らわしい文字 (I, l, O) を除外する。
- **パフォーマンス:** グループ数が非常に多くなった場合、`QuestionGroupUniqueCodeExistsQuery` のパフォーマンスが問題になる可能性がある。その場合は、`UniqueCode` をキーにした別の参照用プロジェクション (例: `Dictionary<string, Guid>`) を作成することを検討する。
- **エラーハンドリング:** API および UI で、`UniqueCode` の重複エラーをユーザーに分かりやすく表示する。
- **既存データ:** 既存の `QuestionGroup` には `UniqueCode` がないため、マイグレーション処理が必要になる可能性がある (例: 起動時に既存グループにユニークコードを割り当てるバッチ処理)。ただし、今回のタスク範囲外とする。
- **コマンドハンドラの移行:** `CreateQuestionGroup` の `Handle` ロジックはワークフローから呼び出される前提で修正した。
