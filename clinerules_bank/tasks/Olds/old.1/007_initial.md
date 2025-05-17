EsCQRSQuestions/EsCQRSQuestions.ApiService/InitialQuestionsService.cs
これですが、起動時に自動ではなく、必要な時にコマンドで送れるようにしたい。

Aspire Host にコマンドを追加して実行できる様にして

https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/custom-resource-commands

そのために、コマンドはHTTP POSTで実行できる様にする

ただ、このタスクでは計画するだけです。
このファイルの下部に計画をよく考えて追記で記入してください。必要なファイルの読み込みなど調査を行い、できるだけ具体的に計画してください。

チャットではなく、このファイル

clinerules_bank/tasks/007_initial.md

を編集して、下に現在の設計を書いてください。
+++++++++++以下に計画を書く+++++++++++

# 初期質問設定の実行コマンド化設計計画

## 現状の分析

現在、`InitialQuestionsService`はホストサービスとして実装されており、APIサービス起動時に自動的に実行されます。このサービスは、一連の質問を作成するロジックを含んでいます。このロジックを必要なときにコマンドで呼び出せるようにする必要があります。

## 変更計画

### 1. InitialQuestionsServiceの改修

現在の`InitialQuestionsService`クラスの機能を以下のように分割します：

1. **質問作成ロジックの分離**：
   - `InitialQuestionsCreator`という新しいクラスを作成し、質問作成ロジックをここに移行します。
   - `CreateInitialQuestions`メソッドを公開して、外部から呼び出せるようにします。

```csharp
public class InitialQuestionsCreator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InitialQuestionsCreator> _logger;

    public InitialQuestionsCreator(
        IServiceProvider serviceProvider,
        ILogger<InitialQuestionsCreator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task CreateInitialQuestions(CancellationToken cancellationToken = default)
    {
        // 既存のInitialQuestionsServiceのStartAsyncメソッドからロジック移行
        // ...質問作成ロジック...
    }

    // 既存のCreateQuestionIfExistsメソッドも移行
}
```

2. **HostedServiceの削除または無効化**：
   - `InitialQuestionsService`のホストサービスは削除するか、条件付きで登録するよう修正します。

### 2. API エンドポイントの追加

1. **InitialQuestionsControllerの作成**：
   - 質問作成を実行するためのHTTP POSTエンドポイントを作成します。

```csharp
// Program.csに追加するエンドポイント
apiRoute
    .MapPost(
        "/system/createInitialQuestions",
        async (
            [FromServices] InitialQuestionsCreator creator,
            CancellationToken cancellationToken) => 
        {
            await creator.CreateInitialQuestions(cancellationToken);
            return Results.Ok(new { message = "Initial questions created successfully" });
        })
    .WithName("CreateInitialQuestions")
    .WithOpenApi();
```

### 3. Aspire Host にカスタムコマンドの追加

1. **カスタムリソースコマンドの追加**：
   - `EsCQRSQuestions.AppHost`プロジェクトの`Program.cs`に、APIサービスのカスタムコマンドを追加します。

```csharp
// ApiServiceリソースにカスタムコマンドを追加
var apiService = builder.AddProject<EsCQRSQuestions_ApiService>("apiservice")
    .WithReference(postgres)
    .WithReference(orleans)
    .WithEndpoint("https")
    .WithCustomResourceCommand("create-initial-questions", "Creates initial questions in the system")
    .ConfigureHttpCommand((command, context) =>
    {
        // POST /system/createInitialQuestions を呼び出す
        var url = context.GetEndpoint("https") + "/system/createInitialQuestions";
        command.HttpPost(url);
    });
```

### 4. DI設定の変更

1. **サービス登録の変更**：
   - `Program.cs`のサービス登録部分を更新して、`InitialQuestionsService`の代わりに`InitialQuestionsCreator`を登録します。

```csharp
// ホストサービスの登録を削除
// builder.Services.AddHostedService<InitialQuestionsService>();

// 代わりに新しいクラスを登録
builder.Services.AddTransient<InitialQuestionsCreator>();
```

## 変更するファイル

1. `EsCQRSQuestions.ApiService/InitialQuestionsService.cs`
   - ロジックを抽出して新しいクラスを作成

2. `EsCQRSQuestions.ApiService/InitialQuestionsCreator.cs` (新規)
   - 質問作成ロジックを含む新しいクラス

3. `EsCQRSQuestions.ApiService/Program.cs`
   - APIエンドポイント追加
   - DIサービス登録変更

4. `EsCQRSQuestions.AppHost/Program.cs`
   - カスタムリソースコマンドの追加

## テスト計画

1. `dotnet aspire createinitialquestions` コマンドが正常に機能するかテスト
2. APIエンドポイントが直接呼び出せるかテスト
3. 質問が正しく作成されるかテスト

## 追加考慮点

- セキュリティ：運用環境では、このエンドポイントは管理者のみがアクセスできるように保護する必要がある
- 冪等性：複数回実行しても問題ないように、既存の質問を確認するロジックを維持する
- ログ出力：コマンド実行状況が明確にわかるようにログを強化する
