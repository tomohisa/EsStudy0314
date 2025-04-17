# Questions Listと質問グループUIの改善設計

## 現状の問題点

1. **グループ選択時のUIの問題**：
   - 現在、グループが選択されている場合のみ、そのグループに属する質問リスト（`GroupQuestionsList`）が表示される
   - グループが選択されていない場合は全質問一覧（`AllQuestionsWithGroupInfoList`）が表示される
   - これにより、ユーザは質問のコンテキスト（どのグループに属しているか）を常に把握できない

2. **質問作成時のグループ選択**：
   - 現在、質問作成時のグループ選択は必須ではなく、ユーザが選択しない場合はデフォルト値が使用される
   - これにより、意図せずグループなしの質問が作成される可能性がある

3. **順序変更機能**：
   - 順序変更機能はグループ選択時のみ使用可能となっている
   - グループを選択しないと、質問の順序付けができない

## 改善計画

### 1. UIレイアウトの変更

1. **常にグループ選択を必須にする**：
   - アプリ起動時にデフォルトで最初のグループを選択状態にする
   - グループがない場合は、質問作成機能を無効化し、ユーザーにグループ作成を促す

2. **グループ選択UIの改良**：
   - グループ選択コンポーネントをより目立たせる
   - 選択中のグループを視覚的に強調表示する

3. **質問リストの表示方法の変更**：
   - `AllQuestionsWithGroupInfoList`コンポーネントの使用を廃止し、常に`GroupQuestionsList`を使用する
   - 選択されているグループの質問だけを常に表示する

### 2. コードの修正内容

#### `Planning.razor`の変更

```csharp
// 初期化時に利用可能なグループを確認
protected override async Task OnInitializedAsync()
{
    // 既存の初期化コード
    
    await RefreshGroups();
    
    // グループが存在する場合は最初のグループを選択
    if (groups?.Any() == true)
    {
        selectedGroupId = groups.First().Id;
        await RefreshQuestionsInGroup();
    }
    // グループが存在しない場合は選択なし - 質問作成は無効化される
}
```

#### UIコンポーネントの変更

```razor
<!-- グループ選択が常に存在するようにし、選択されたグループの質問のみを表示 -->
<div class="row mb-4">
    <div class="col">
        <QuestionGroupList 
            Groups="groups" 
            SelectedGroupId="selectedGroupId" 
            OnGroupSelected="SelectGroup" 
            OnCreateGroupClicked="OpenCreateGroupModal" />
    </div>
</div>

<!-- 常にGroupQuestionsListを表示 -->
@if (selectedGroupId.HasValue && groups?.Any() == true)
{
    <GroupQuestionsList 
        QuestionsInGroup="questionsInGroup" 
        GroupId="selectedGroupId.Value" 
        GroupName="@(groups?.FirstOrDefault(g => g.Id == selectedGroupId)?.Name ?? "Selected Group")" 
        GetResponseCount="GetResponseCountForQuestion" 
        GetQuestionOrderInGroup="GetQuestionOrderInGroup" 
        OnViewQuestion="ViewQuestionDetails" 
        OnEditQuestion="OpenEditQuestionModal" 
        OnStartDisplay="StartDisplayQuestion" 
        OnStopDisplay="StopDisplayQuestion" 
        OnDeleteQuestion="DeleteQuestion" 
        OnEditGroup="() => OpenEditGroupModal(selectedGroupId.Value)" 
        OnDeleteGroup="() => DeleteGroup(selectedGroupId.Value)" 
        OnAddQuestion="() => OpenCreateQuestionInGroupModal(selectedGroupId.Value)" 
        OnChangeQuestionOrder="HandleChangeQuestionOrder" />
}
else
{
    <div class="alert alert-info">
        Please select or create a question group.
    </div>
}
```

#### 質問作成・編集時のグループ選択の変更

```csharp
// OpenCreateQuestionModalの修正 - グループ必須化
private async Task OpenCreateQuestionModal()
{
    // グループが選択されていない場合は処理を中止
    if (!selectedGroupId.HasValue)
    {
        await JsRuntime.InvokeVoidAsync("alert", "Please select a question group first.");
        return;
    }

    isEditMode = false;
    editQuestionId = null;
    questionModel = new QuestionEditModel
    {
        QuestionGroupId = selectedGroupId.Value, // 現在選択されているグループを強制的に設定
        Options = new List<QuestionOptionEditModel>
        {
            new QuestionOptionEditModel { Id = "1", Text = "" },
            new QuestionOptionEditModel { Id = "2", Text = "" }
        }
    };
    await ShowQuestionModal();
}

// SaveQuestionの修正 - グループIDのデフォルト値を使用しない
private async Task SaveQuestion()
{
    try
    {
        // グループIDが空の場合はエラーとする
        if (questionModel.QuestionGroupId == Guid.Empty)
        {
            await JsRuntime.InvokeVoidAsync("alert", "Question group is required. Please select a group.");
            return;
        }

        Console.WriteLine("Saving question...");
        var options = questionModel.Options.Select(o => new QuestionOption(o.Id, o.Text)).ToList();
        
        // グループIDは必ずモデルから取得
        var groupIdToUse = questionModel.QuestionGroupId;
        
        Console.WriteLine($"Selected group for question: {groupIdToUse}");
        
        // 残りの保存ロジック...
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error saving question: {ex.Message}");
    }
}
```

### 3. QuestionFormModalコンポーネントの修正

`QuestionFormModal.razor`コンポーネントを修正して、グループ選択フィールドを必須にし、UIでも必須であることを示す。

```razor
<div class="mb-3">
    <label for="questionGroup" class="form-label">Question Group <span class="text-danger">*</span></label>
    <select id="questionGroup" class="form-select" @bind="Model.QuestionGroupId" required>
        <option value="">-- Select a Group --</option>
        @foreach (var group in AvailableGroups ?? new List<GetQuestionGroupsQuery.ResultRecord>())
        {
            <option value="@group.Id">@group.Name</option>
        }
    </select>
    <div class="invalid-feedback">Please select a question group.</div>
</div>
```

### 4. 順序変更機能の改善

GroupQuestionsListコンポーネントにて、質問の順序を直感的に変更できるようUIを強化する。

```razor
<div class="mb-2">
    <div class="d-flex align-items-center">
        <span class="me-2">Order: @question.Order</span>
        <div class="btn-group btn-group-sm">
            <button class="btn btn-outline-secondary" @onclick="() => OnChangeQuestionOrder.InvokeAsync((question.QuestionId, question.Order - 1))" disabled="@(question.Order <= 0)">
                <i class="bi bi-arrow-up"></i>
            </button>
            <button class="btn btn-outline-secondary" @onclick="() => OnChangeQuestionOrder.InvokeAsync((question.QuestionId, question.Order + 1))" disabled="@(question.Order >= Questions.Count - 1)">
                <i class="bi bi-arrow-down"></i>
            </button>
        </div>
    </div>
</div>
```

## 期待される改善効果

1. **ユーザー操作の明確化**：
   - 質問はすべて特定のグループに所属することが明示され、ユーザは常にコンテキストを意識できる
   - グループ選択が必須となり、意図しないグループ割り当てを防止

2. **一貫性のある表示**：
   - 常に選択されたグループの質問のみが表示されるため、一覧がシンプルで理解しやすくなる
   - 選択中のグループのコンテキストが常に維持される

3. **順序管理の向上**：
   - グループ選択が常に行われるため、順序変更機能が常に利用可能になる
   - 直感的な上下操作による順序変更が可能になる

## 実装の流れ

1. Planning.razor の初期化ロジックの修正（デフォルトグループ選択を追加）
2. UI表示ロジックの変更（常にGroupQuestionsListを使用）
3. 質問作成・編集ロジックの修正（グループ選択を必須化）
4. QuestionFormModalコンポーネントの修正（グループ選択UIの強化）
5. 順序変更機能の改善（より直感的なUI）
6. テストとデバッグ
