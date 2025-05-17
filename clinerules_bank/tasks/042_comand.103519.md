# GitHub Copilot

## LastSortableUniqueIdを返すようにするための計画

タスク: EsCQRSQuestions/EsCQRSQuestions.ApiService/Program.csのコマンドや、Workflow内部でコマンドを実行するものが、LastSortableUniqueIdを返すように修正する計画を立てる。

### 現状分析

現在のコードを分析した結果、以下の点がわかりました：

1. シンプルなコマンド実行では `ToSimpleCommandResponse()` をつけることで、LastSortableUniqueIdを含む簡易レスポンスを返せる
2. しかし、ワークフロー内部でコマンドを実行する場合、この変換が行われていない
3. 主に以下のワークフローが関係しています：
   - QuestionGroupWorkflow
   - QuestionDisplayWorkflow

### 修正方針

ワークフローのメソッドが LastSortableUniqueId を含むレスポンスを返すように修正する必要があります。修正のアプローチとしては、以下のパターンを適用します：

1. ワークフローメソッドの戻り値型を統一する
2. コマンド実行結果から `ToSimpleCommandResponse()` を通して LastSortableUniqueId を取得する
3. 複数のコマンドを実行するワークフローでは、最後のコマンドの LastSortableUniqueId を返す

### 修正対象メソッド

#### QuestionGroupWorkflow

1. `CreateGroupWithQuestionsAsync` メソッド
   - 現在: `Task<ResultBox<Guid>>`
   - 修正後: `Task<ResultBox<CommandResponseSimple>>`
   - 最後の AddQuestionToGroup コマンドの ToSimpleCommandResponse を返す

2. `CreateQuestionAndAddToGroupAsync` メソッド
   - 現在: `Task<ResultBox<Guid>>` と `Task<ResultBox<bool>>`
   - 修正後: `Task<ResultBox<CommandResponseSimple>>`
   - AddQuestionToGroup コマンドの ToSimpleCommandResponse を返す

3. `MoveQuestionBetweenGroupsAsync` メソッド
   - 現在: `Task<ResultBox<bool>>`
   - 修正後: `Task<ResultBox<CommandResponseSimple>>`
   - UpdateQuestionGroupIdCommand コマンドの ToSimpleCommandResponse を返す

4. `CreateGroupWithUniqueCodeAsync` メソッド
   - 現在: `Task<ResultBox<Guid>>`
   - 修正後: `Task<ResultBox<CommandResponseSimple>>`
   - CreateQuestionGroup コマンドの ToSimpleCommandResponse を返す

#### QuestionDisplayWorkflow

1. `StartDisplayQuestionExclusivelyAsync` メソッド
   - 現在: `Task<ResultBox<object>>`
   - 修正後: `Task<ResultBox<CommandResponseSimple>>`
   - StartDisplayCommand コマンドの ToSimpleCommandResponse を返す

### Program.cs での利用方法

Program.cs でワークフローを使用しているエンドポイントも修正が必要です。主に以下のエンドポイントが対象になります：

1. `/questions/create` エンドポイント
   - 現在のワークフロー呼び出し: `workflow.CreateQuestionAndAddToGroupEndAsync(command).UnwrapBox()`
   - 修正後: そのまま `.UnwrapBox()` で CommandResponseSimple が返される

2. `/questions/startDisplay` エンドポイント
   - 現在のワークフロー呼び出し: `workflow.StartDisplayQuestionExclusivelyAsync(command.QuestionId).UnwrapBox()`
   - 修正後: そのまま `.UnwrapBox()` で CommandResponseSimple が返される

3. `/questionGroups/createWithUniqueCode` エンドポイント
   - 現在の実装: ResultBox<Guid> を受け取り、それを JSON オブジェクトに変換
   - 修正後: CommandResponseSimple を受け取り、必要に応じて AggregateId などの情報をレスポンスに含める

4. `/questionGroups/createWithQuestions` エンドポイント
   - 現在の実装: ResultBox<Guid> を受け取り、それを JSON オブジェクトに変換
   - 修正後: CommandResponseSimple を受け取り、必要に応じて AggregateId などの情報をレスポンスに含める

### 実装ステップ

1. まず、ワークフローの返却型を `ResultBox<CommandResponseSimple>` に変更する
2. 各ワークフローメソッド内で、最終コマンド実行後に `.ToSimpleCommandResponse()` を呼び出す
3. Program.cs のエンドポイントでは戻り値をそのまま返すか、必要に応じて AggregateId や他の情報と組み合わせて返す
4. テストを実行し、クライアント側でも LastSortableUniqueId を利用できることを確認する

### 注意点

1. ワークフロー内の中間コマンドに対しても、エラー処理を適切に行う必要がある
2. クライアント側でも LastSortableUniqueId を活用できるようにAPIクライアントの修正が必要になる可能性がある
3. 複数のコマンドを実行するワークフローでは、最後のコマンドの LastSortableUniqueId を返すようにする

この計画に基づいて実装を進めることで、一貫性のあるAPI応答パターンを実現し、クライアント側での状態管理が改善されると考えられます。😊
