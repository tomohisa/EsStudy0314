モデル名: GitHub Copilot

# 管理画面の回答表示パーセンテージ修正計画

## 問題の概要

現在、EsCQRSQuestions.AdminWeb の Planning.razor ページ内の QuestionDetail コンポーネントでは、回答のパーセンテージ計算が「選択数」ベースで行われています。これを前回のタスク（032_aggregation_bug）で修正した回答側（ユーザー側）と同様に「ユーザー数」ベースの計算に変更する必要があります。

## 現在の実装

現在の実装では、QuestionDetail.razor の中で以下のように計算されています：

```csharp
var count = SelectedQuestion.Responses.Count(r => r.SelectedOptionId == option.Id);
var percentage = SelectedQuestion.Responses.Any() 
    ? (count * 100.0 / SelectedQuestion.Responses.Count) 
    : 0;
```

この計算方法では、特定のオプションの選択数を総回答数で割っているため、1人のユーザーが複数回答した場合に各回答のパーセンテージの合計が100%を超えないようになっています（例：50%）。

## 修正計画

### 1. EsCQRSQuestions.AdminWeb/Components/Planning/QuestionDetail.razor の修正

QuestionDetail.razor の Response Statistics セクションを修正し、以下のようにパーセンテージの計算方法を変更します：

1. 各オプションを選択したユニークな参加者（ユーザー）の数を計算
2. ユニークな参加者の総数を分母として使用
3. パーセンテージを「特定のオプションを選択したユニークなユーザー数 ÷ ユニークなユーザーの総数」で計算

具体的な修正コード：

```csharp
// ユニークな参加者のIDまたは名前のセットを取得
var uniqueParticipants = SelectedQuestion.Responses
    .Select(r => r.ParticipantId)  // または適切なユーザー識別子
    .Distinct()
    .ToList();

var totalUniqueParticipants = uniqueParticipants.Count;

foreach (var option in SelectedQuestion.Options)
{
    // このオプションを選択したユニークなユーザー数を計算
    var uniqueUsersForOption = SelectedQuestion.Responses
        .Where(r => r.SelectedOptionId == option.Id)
        .Select(r => r.ParticipantId)  // または適切なユーザー識別子
        .Distinct()
        .Count();

    // パーセンテージを計算
    var percentage = totalUniqueParticipants > 0 
        ? (uniqueUsersForOption * 100.0 / totalUniqueParticipants) 
        : 0;
    
    // 表示処理...
}
```

### 2. 調査が必要な点

実装を進める前に以下の点を確認する必要があります：

1. Response オブジェクトに `ParticipantId` または他のユニークなユーザー識別子があるのか
2. 複数回答を許可している場合、どのようにユーザーを識別するのか
3. 匿名ユーザーの場合はどのように扱うか

## 実装手順

1. QuestionDetailQuery.QuestionDetailRecord クラスとその中の Response オブジェクトの構造を確認する
2. ユーザーを識別するための適切なフィールドを特定する
3. QuestionDetail.razor の Response Statistics セクションのコードを修正する
4. テストを行い、パーセンテージが正しく表示されることを確認する

## 追加の考慮事項

- ユーザー識別子がない場合は、代替手段（例：IPアドレスやセッションID）を検討する
- パフォーマンスへの影響を考慮して、可能であればサーバー側で計算を行うことも検討する
- 表示方法についても、現在の水平バーグラフが最適かどうか再検討する（例：円グラフの方が直感的に理解しやすい場合もある）

以上の計画に基づいて実装を進めることで、管理画面でも回答側と同様に、ユーザー数ベースのパーセンテージ計算が実現できます。