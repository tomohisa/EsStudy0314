# Cline - 修正計画 🐧

## 現在の問題点

`EsCQRSQuestions/EsCQRSQuestions.Domain/Services/IQuestionGroupService.cs` に実装されているコードには以下の問題があります：

1. インターフェース（IQuestionGroupService）が不要に作成されている
2. QuestionGroupServiceが`SekibanOrleansExecutor`を直接DIしており、`ISekibanExecutor`を使うべき
3. Workflowパターンに従っていない（QuestionGroupWorkflowと同様のパターンを使用するべき）
4. 使用方法が間違っている（DIから直接サービスを取得するのではなく、SekibanOrleansExecutorを取得してQuestionGroupServiceを生成する必要がある）

## 現在の実装

### IQuestionGroupService.cs
現在のファイルでは：
- `IQuestionGroupService`インターフェースと`QuestionGroupService`実装クラスが同じファイルに定義されている
- `QuestionGroupService`は`SekibanOrleansExecutor`（具体的な実装クラス）に依存している
- DIでインターフェースと実装が登録されている

### Program.cs
現在のファイルでは：
```csharp
// Register QuestionGroupService
builder.Services.AddTransient<EsCQRSQuestions.Domain.Services.IQuestionGroupService, EsCQRSQuestions.Domain.Services.QuestionGroupService>();
```

使用部分：
```csharp
apiRoute.MapGet("/questions/active", async (
        [FromServices] SekibanOrleansExecutor executor,
        [FromServices] EsCQRSQuestions.Domain.Services.IQuestionGroupService groupService,
        [FromQuery] string? uniqueCode = null) => {
        // ... (ここでgroupServiceを使用)
```

## 修正計画

### 1. IQuestionGroupService.csの修正
`IQuestionGroupService`インターフェースを削除し、`QuestionGroupService`クラスだけを残します。DI注入を`ISekibanExecutor`に変更します：

```csharp
using Sekiban.Pure.Executors;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using ResultBoxes;

namespace EsCQRSQuestions.Domain.Services;

/// <summary>
/// グループ情報を扱うサービス
/// </summary>
public class QuestionGroupService
{
    private readonly ISekibanExecutor _executor;
    
    public QuestionGroupService(ISekibanExecutor executor)
    {
        _executor = executor;
    }
    
    /// <summary>
    /// UniqueCodeからグループIDを取得する
    /// </summary>
    public async Task<Guid?> GetGroupIdByUniqueCodeAsync(string uniqueCode)
    {
        if (string.IsNullOrWhiteSpace(uniqueCode))
        {
            return null;
        }
        
        var groupsResult = await _executor.QueryAsync(new GetQuestionGroupsQuery());
        if (!groupsResult.IsSuccess)
        {
            return null;
        }
        
        var groups = groupsResult.GetValue();
        var group = groups.Items.FirstOrDefault(g => g.UniqueCode == uniqueCode);
        return group?.Id;
    }
    
    /// <summary>
    /// すべてのグループのID・UniqueCodeのペアを取得する
    /// </summary>
    public async Task<IEnumerable<(Guid Id, string UniqueCode)>> GetAllGroupsAsync()
    {
        var groupsResult = await _executor.QueryAsync(new GetQuestionGroupsQuery());
        if (!groupsResult.IsSuccess)
        {
            return Enumerable.Empty<(Guid, string)>();
        }
        
        var groups = groupsResult.GetValue();
        return groups.Items.Select(g => (g.Id, g.UniqueCode));
    }
}
```

### 2. Program.csの修正

DIからサービスの登録を削除し、使用箇所でSekibanOrleansExecutorを使ってQuestionGroupServiceを生成する方式に変更します：

```csharp
// 以下の行を削除
// builder.Services.AddTransient<EsCQRSQuestions.Domain.Services.IQuestionGroupService, EsCQRSQuestions.Domain.Services.QuestionGroupService>();
```

使用箇所の修正：
```csharp
apiRoute.MapGet("/questions/active", async (
        [FromServices] SekibanOrleansExecutor executor,
        [FromQuery] string? uniqueCode = null) =>
    {
        // QuestionGroupServiceをその場で生成
        var groupService = new QuestionGroupService(executor);
        
        // UniqueCodeからグループIDを取得
        Guid? groupId = null;
        if (!string.IsNullOrWhiteSpace(uniqueCode))
        {
            groupId = await groupService.GetGroupIdByUniqueCodeAsync(uniqueCode);
        }
        
        // 残りのコードは同じ
        // ...
    }
```

## 実装の利点

1. **依存性の軽減**: 具体的な実装（SekibanOrleansExecutor）から抽象（ISekibanExecutor）に依存することで、テストやメンテナンスが容易になります
2. **一貫性の向上**: 他のWorkflowクラスと同じパターンを採用することで、コードベース全体の一貫性が向上します
3. **柔軟性の向上**: 必要な時に必要な場所でサービスを生成できるようになり、より柔軟な設計になります

この修正により、コードの一貫性が向上し、より保守しやすいプロジェクト構造になります。また、テスト時にモックを使用しやすくなるため、テスト容易性も向上します。
