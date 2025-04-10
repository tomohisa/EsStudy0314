clinerules_bank/tasks/005_order.md
で設計した集約をUIにしたいです。

今のページは、グループの概念なしで作られています。
EsCQRSQuestions/EsCQRSQuestions.AdminWeb/Components/Pages/Planning.razor

グループの概念を導入することによって、グループ選択もしくはグループの作成が最初に来ないといけなくなります。別ページでもいいですし、同じページでグループ分けするのでも良いのでUIを更新してください。APIなど必要なものがあれば作ってください。

ただ、このタスクでは計画するだけです。
このファイルの下部に計画をよく考えて追記で記入してください。必要なファイルの読み込みなど調査を行い、できるだけ具体的に計画してください。

チャットではなく、このファイル

clinerules_bank/tasks/006_QuestionGroupUI.md

を編集して、下に現在の設計を書いてください。
+++++++++++以下に計画を書く+++++++++++

# Question Group UI 実装計画

## 1. 概要

現在の `Planning.razor` は質問をグループなしで管理していますが、task 005 で設計した QuestionGroup 集約を UI に統合する必要があります。この計画では、管理画面に質問グループの概念を導入し、質問をグループ内で管理できるようにします。

## 2. UI設計アプローチ

### 2.1. 同一ページでの実装 (推奨)

現在の `Planning.razor` ページ内にグループ選択と管理機能を追加します。この方法では:
- 最初にグループ一覧を表示
- グループが選択されたら、そのグループ内の質問を表示・管理できる
- グループの作成、編集、削除機能も同じページに統合

この方式のメリット:
- ページ遷移なしで一貫した UI を提供できる
- 既存のコードを活かしつつ拡張できる

### 2.2. 別ページでの実装 (代替案)

2つの別々のページに分割する方法:
- `/groups` - グループ一覧と管理
- `/groups/{groupId}/questions` - 特定グループ内の質問管理

この方式は、機能が明確に分離されるメリットがありますが、ページ間の遷移が必要になります。

## 3. 実装計画詳細 (同一ページ方式)

### 3.1. バックエンド実装

#### 3.1.1. QuestionGroupApiClient の作成
`EsCQRSQuestions.AdminWeb/QuestionGroupApiClient.cs` を作成し、以下のメソッドを実装:

```csharp
public class QuestionGroupApiClient(HttpClient httpClient)
{
    // グループ一覧の取得
    public async Task<List<GetQuestionGroupsQuery.ResultRecord>> GetQuestionGroupsAsync(CancellationToken cancellationToken = default)
    
    // 特定グループの詳細取得
    public async Task<GetQuestionGroupsQuery.ResultRecord?> GetQuestionGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    
    // グループ内の質問一覧取得
    public async Task<List<GetQuestionsByGroupIdQuery.ResultRecord>> GetQuestionsInGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    
    // 新しいグループの作成
    public async Task CreateQuestionGroupAsync(string name, CancellationToken cancellationToken = default)
    
    // グループ名の更新
    public async Task UpdateQuestionGroupNameAsync(Guid groupId, string newName, CancellationToken cancellationToken = default)
    
    // グループの削除
    public async Task DeleteQuestionGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    
    // 質問をグループに追加
    public async Task AddQuestionToGroupAsync(Guid groupId, Guid questionId, int order, CancellationToken cancellationToken = default)
    
    // 質問の順序変更
    public async Task ChangeQuestionOrderAsync(Guid groupId, Guid questionId, int newOrder, CancellationToken cancellationToken = default)
    
    // 質問をグループから削除
    public async Task RemoveQuestionFromGroupAsync(Guid groupId, Guid questionId, CancellationToken cancellationToken = default)
}
```

#### 3.1.2. DI への登録

`Program.cs` に QuestionGroupApiClient の登録を追加:

```csharp
builder.Services.AddHttpClient<QuestionGroupApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
});
```

### 3.2. フロントエンド実装

#### 3.2.1. Planning.razor の更新

既存の `Planning.razor` を変更して、グループ管理機能を追加します:

```razor
@page "/planning"
@attribute [StreamRendering(true)]
@using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads
@using EsCQRSQuestions.Domain.Aggregates.Questions.Queries
@using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries
@inject QuestionApiClient QuestionApi
@inject QuestionGroupApiClient QuestionGroupApi
@inject ActiveUsersApiClient ActiveUsersApi
@inject IJSRuntime JsRuntime

<PageTitle>Question Management</PageTitle>

<!-- ヘッダー部分 (既存コードを維持) -->

<!-- グループ選択UI -->
<div class="row mb-4">
    <div class="col-md-8">
        <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
                <h5 class="mb-0">Question Groups</h5>
                <button class="btn btn-primary btn-sm" @onclick="OpenCreateGroupModal">Create New Group</button>
            </div>
            <div class="card-body">
                @if (groups == null)
                {
                    <p><em>Loading groups...</em></p>
                }
                else if (!groups.Any())
                {
                    <div class="alert alert-info">
                        No question groups found. Create a group to get started.
                    </div>
                }
                else
                {
                    <div class="list-group">
                        @foreach (var group in groups)
                        {
                            <button type="button" 
                                    class="list-group-item list-group-item-action d-flex justify-content-between align-items-center @(selectedGroupId == group.Id ? "active" : "")"
                                    @onclick="() => SelectGroup(group.Id)">
                                @group.Name
                                <span class="badge bg-primary rounded-pill">@group.Questions.Count</span>
                            </button>
                        }
                    </div>
                }
            </div>
        </div>
    </div>
</div>

<!-- グループが選択されている場合、その中の質問を表示 -->
@if (selectedGroupId.HasValue)
{
    <div class="mb-4">
        <div class="d-flex justify-content-between align-items-center">
            <h2>
                Questions in Group: @(groups?.FirstOrDefault(g => g.Id == selectedGroupId)?.Name ?? "")
            </h2>
            <div>
                <button class="btn btn-outline-secondary me-2" @onclick="() => OpenEditGroupModal(selectedGroupId.Value)">
                    <i class="bi bi-pencil"></i> Edit Group
                </button>
                <button class="btn btn-danger me-2" @onclick="() => DeleteGroup(selectedGroupId.Value)">
                    <i class="bi bi-trash"></i> Delete Group
                </button>
                <button class="btn btn-primary" @onclick="() => OpenCreateQuestionModal(selectedGroupId.Value)">
                    <i class="bi bi-plus"></i> Add Question
                </button>
            </div>
        </div>
    </div>
    
    <!-- 質問一覧表示 (既存UIを修正) -->
    @if (questionsInGroup == null)
    {
        <p><em>Loading questions...</em></p>
    }
    else if (!questionsInGroup.Any())
    {
        <div class="alert alert-info">
            No questions in this group. Create a new question to get started.
        </div>
    }
    else
    {
        <div class="table-responsive">
            <!-- 既存の質問テーブルと同様のテーブル、ただし順序変更機能を追加 -->
        </div>
    }
}
else
{
    <!-- グループが選択されていない場合のメッセージ -->
    <div class="alert alert-info">
        Please select a question group from the list above or create a new group.
    </div>
}

<!-- 質問詳細表示部分 (既存コードを修正) -->

<!-- グループ作成/編集モーダル -->
<div class="modal fade" id="groupModal" tabindex="-1" aria-labelledby="groupModalLabel" aria-hidden="true">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title" id="groupModalLabel">@(isEditGroupMode ? "Edit Group" : "Create New Group")</h5>
                <button type="button" class="btn-close" @onclick="CloseGroupModal" aria-label="Close"></button>
            </div>
            <div class="modal-body">
                <div class="mb-3">
                    <label for="groupName" class="form-label">Group Name</label>
                    <input type="text" class="form-control" id="groupName" @bind="groupModel.Name" />
                    @if (!string.IsNullOrEmpty(groupModel.NameError))
                    {
                        <div class="text-danger">@groupModel.NameError</div>
                    }
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" @onclick="CloseGroupModal">Cancel</button>
                <button type="button" class="btn btn-primary" @onclick="SaveGroup">Save</button>
            </div>
        </div>
    </div>
</div>

<!-- 質問作成/編集モーダル (既存コードを修正) -->
```

#### 3.2.2. 追加するコンポーネントロジック

`@code` ブロックに以下の変数とメソッドを追加:

```csharp
// グループ関連の変数
private List<GetQuestionGroupsQuery.ResultRecord>? groups;
private Guid? selectedGroupId;
private List<GetQuestionsByGroupIdQuery.ResultRecord>? questionsInGroup;
private bool isEditGroupMode = false;
private Guid? editGroupId;
private GroupModel groupModel = new();

// 初期ロード時にグループも取得
protected override async Task OnInitializedAsync()
{
    // 既存の初期化コード...
    
    // グループ関連の初期化
    await RefreshGroups();
}

// グループ一覧の取得
private async Task RefreshGroups()
{
    try
    {
        Console.WriteLine("Refreshing question groups...");
        groups = await QuestionGroupApi.GetQuestionGroupsAsync();
        Console.WriteLine($"Fetched {groups.Count} question groups");
        await InvokeAsync(() => StateHasChanged());
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error refreshing question groups: {ex.Message}");
    }
}

// グループの選択
private async Task SelectGroup(Guid groupId)
{
    selectedGroupId = groupId;
    await RefreshQuestionsInGroup();
}

// グループ内の質問を取得
private async Task RefreshQuestionsInGroup()
{
    if (selectedGroupId.HasValue)
    {
        try
        {
            Console.WriteLine($"Refreshing questions in group {selectedGroupId.Value}...");
            questionsInGroup = await QuestionGroupApi.GetQuestionsInGroupAsync(selectedGroupId.Value);
            Console.WriteLine($"Fetched {questionsInGroup.Count} questions in group");
            await InvokeAsync(() => StateHasChanged());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error refreshing questions in group: {ex.Message}");
        }
    }
}

// グループ作成モーダルを開く
private async Task OpenCreateGroupModal()
{
    isEditGroupMode = false;
    editGroupId = null;
    groupModel = new GroupModel();
    await JsRuntime.InvokeVoidAsync("showModal", "groupModal");
}

// グループ編集モーダルを開く
private async Task OpenEditGroupModal(Guid groupId)
{
    isEditGroupMode = true;
    editGroupId = groupId;
    
    var group = groups?.FirstOrDefault(g => g.Id == groupId);
    if (group != null)
    {
        groupModel = new GroupModel
        {
            Name = group.Name
        };
        await JsRuntime.InvokeVoidAsync("showModal", "groupModal");
    }
}

// グループの保存
private async Task SaveGroup()
{
    // バリデーション
    bool isValid = true;
    
    if (string.IsNullOrWhiteSpace(groupModel.Name))
    {
        groupModel.NameError = "Group name is required";
        isValid = false;
    }
    else
    {
        groupModel.NameError = null;
    }
    
    if (!isValid)
    {
        return;
    }
    
    try
    {
        Console.WriteLine("Saving question group...");
        
        if (isEditGroupMode && editGroupId.HasValue)
        {
            await QuestionGroupApi.UpdateQuestionGroupNameAsync(editGroupId.Value, groupModel.Name);
            Console.WriteLine("Question group updated");
        }
        else
        {
            await QuestionGroupApi.CreateQuestionGroupAsync(groupModel.Name);
            Console.WriteLine("Question group created");
        }
        
        await CloseGroupModal();
        
        // 手動でグループリストを更新
        await RefreshGroups();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error saving question group: {ex.Message}");
    }
}

// グループモーダルを閉じる
private async Task CloseGroupModal()
{
    await JsRuntime.InvokeVoidAsync("hideModal", "groupModal");
}

// グループの削除
private async Task DeleteGroup(Guid groupId)
{
    if (await JsRuntime.InvokeAsync<bool>("confirm", "Are you sure you want to delete this group? All questions in this group will be affected."))
    {
        try
        {
            Console.WriteLine($"Deleting question group {groupId}...");
            await QuestionGroupApi.DeleteQuestionGroupAsync(groupId);
            Console.WriteLine("Question group deleted");
            
            selectedGroupId = null;
            questionsInGroup = null;
            
            // 手動でグループリストを更新
            await RefreshGroups();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error deleting question group: {ex.Message}");
        }
    }
}

// 質問を開くメソッドを修正して、グループを考慮する
private async Task OpenCreateQuestionModal(Guid groupId)
{
    isEditMode = false;
    editQuestionId = null;
    questionModel = new QuestionModel
    {
        QuestionGroupId = groupId,
        Options = new List<QuestionOptionModel>
        {
            new QuestionOptionModel { Id = "1", Text = "" },
            new QuestionOptionModel { Id = "2", Text = "" }
        }
    };
    await ShowModal();
}

// 質問保存メソッドを修正して、グループIDを含める
// ...

// グループIDの移動機能
private async Task MoveQuestionToGroup(Guid questionId, Guid sourceGroupId, Guid targetGroupId)
{
    try
    {
        // 最後の位置に追加
        int order = groups?.FirstOrDefault(g => g.Id == targetGroupId)?.Questions.Count ?? 0;
        
        var workflowCommand = new QuestionGroupWorkflow.MoveQuestionBetweenGroupsCommand(
            questionId, 
            sourceGroupId, 
            targetGroupId, 
            order
        );
        
        await QuestionGroupApi.MoveQuestionBetweenGroupsAsync(workflowCommand);
        await RefreshGroups();
        await RefreshQuestionsInGroup();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error moving question between groups: {ex.Message}");
    }
}

// 質問の順序変更
private async Task ChangeQuestionOrder(Guid questionId, int newOrder)
{
    if (!selectedGroupId.HasValue) return;
    
    try
    {
        await QuestionGroupApi.ChangeQuestionOrderAsync(selectedGroupId.Value, questionId, newOrder);
        await RefreshQuestionsInGroup();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error changing question order: {ex.Message}");
    }
}

// グループモデル
private class GroupModel
{
    public string Name { get; set; } = "";
    public string? NameError { get; set; }
}

// 質問モデルを拡張
private class QuestionModel
{
    public string Text { get; set; } = "";
    public string? TextError { get; set; }
    public List<QuestionOptionModel> Options { get; set; } = new();
    public string? OptionsError { get; set; }
    public Guid QuestionGroupId { get; set; } // 追加
}
```

#### 3.2.3. ドラッグ&ドロップによる順序変更機能の検討

オプションとして、質問の順序を視覚的にドラッグ&ドロップで変更できる機能を追加することも検討できます。これには次のステップが必要です:

1. JavaScript ライブラリ (SortableJS など) の追加
2. ドラッグ&ドロップ操作時に `ChangeQuestionOrder` メソッドを呼び出す連携機能
3. UI更新のための JSInterop の実装

### 3.3. SignalR イベント通知の拡張

既存の SignalR 接続に、グループ関連イベントのハンドラーを追加します:

```csharp
// Group イベント
hubConnection.On<object>("QuestionGroupCreated", async _ =>
{
    await RefreshGroups();
});

hubConnection.On<object>("QuestionGroupUpdated", async _ =>
{
    await RefreshGroups();
});

hubConnection.On<object>("QuestionGroupDeleted", async _ =>
{
    await RefreshGroups();
    if (selectedGroupId != null && groups != null && groups.All(g => g.Id != selectedGroupId))
    {
        selectedGroupId = null;
        questionsInGroup = null;
    }
});

hubConnection.On<object>("QuestionAddedToGroup", async _ =>
{
    await RefreshGroups();
    if (selectedGroupId.HasValue)
    {
        await RefreshQuestionsInGroup();
    }
});

hubConnection.On<object>("QuestionRemovedFromGroup", async _ =>
{
    await RefreshGroups();
    if (selectedGroupId.HasValue)
    {
        await RefreshQuestionsInGroup();
    }
});

hubConnection.On<object>("QuestionOrderChanged", async _ =>
{
    if (selectedGroupId.HasValue)
    {
        await RefreshQuestionsInGroup();
    }
});
```

### 3.4. Hub の更新

`QuestionHub.cs` を更新して、グループ関連のイベントを送信する機能を追加します。

## 4. リファクタリング検討事項

### 4.1. コンポーネント分割

機能が多くなるため、以下のコンポーネント分割を検討:

- `QuestionGroupList.razor` - グループの一覧表示・管理
- `QuestionList.razor` - 選択されたグループ内の質問一覧
- `QuestionDetails.razor` - 質問の詳細表示
- `CreateEditQuestionModal.razor` - 質問作成/編集モーダル
- `CreateEditGroupModal.razor` - グループ作成/編集モーダル

### 4.2. 状態管理

ページが複雑になるため、状態管理の改善も検討:

- Fluxor などの状態管理ライブラリの導入
- または独自のステートサービスの実装

## 5. テスト計画

1. グループの基本操作:
   - グループの作成、表示、編集、削除
   
2. グループ内の質問管理:
   - グループへの質問追加
   - グループ内の質問表示
   - 質問の順序変更
   - グループからの質問削除
   - 質問のグループ間移動

3. エラー処理:
   - 不正な操作への対応
   - API通信エラーのハンドリング

## 6. 実装手順

1. 必要なモデルとインターフェースの実装
2. QuestionGroupApiClient の実装
3. Planning.razor の基本的な変更 (グループ選択 UI)
4. グループの作成、編集、削除機能の実装
5. グループ内質問のフィルタリング表示
6. 質問の順序変更機能
7. グループ間の質問移動機能
8. SignalR イベント通知の拡張
9. UI/UX の調整とリファクタリング

## 7. 課題と考慮事項

1. 既存の質問データの扱い:
   - 既存の質問をどのグループに配置するか
   - 移行戦略の検討

2. パフォーマンス:
   - 多数のグループや質問がある場合の表示最適化
   - ページネーションの検討

3. UX:
   - 直感的なインターフェースの設計
   - 操作のフィードバックとエラー表示
   
4. データ整合性:
   - グループと質問の関係の一貫性確保
   - 誤操作による不整合の防止

以上の計画に基づき、次のステップではバックエンドAPIとフロントエンド実装に着手します。