# Log Analytics Workspaceのコスト削減計画 - Claude

## 現状分析

現在のLog Analytics Workspace（LAW）の設定を確認したところ、以下の点が高コストの原因となっている可能性があります：

1. **SKU**: `PerGB2018` - 取り込まれるデータ量に基づいて課金される
2. **保持期間**: 30日
3. **大量のログカテゴリが有効化**: すべてのカテゴリ（AppServiceHTTPLogs, AppServiceConsoleLogs, AppServiceAppLogs, AppServiceAuditLogs, AppServiceIPSecAuditLogs, AppServicePlatformLogs）が有効
4. **すべてのメトリクスが有効**: `AllMetrics`が有効
5. **複数のサービスから診断設定**: バックエンド、Blazor、AdminWebからすべて同じLAWにログを送信

## コスト削減のための具体的な計画

### 1. 重要なログカテゴリのみを有効化

不要なログカテゴリを無効にして、取り込むデータ量を削減します：

```bicep
logs: [
  {
    category: 'AppServiceHTTPLogs'
    enabled: true  // 重要なので有効のまま
  }
  {
    category: 'AppServiceConsoleLogs'
    enabled: false // 開発時のみ必要なので無効化
  }
  {
    category: 'AppServiceAppLogs'
    enabled: true  // アプリケーションログは重要なので有効のまま
  }
  {
    category: 'AppServiceAuditLogs'
    enabled: false // 通常は不要なので無効化
  }
  {
    category: 'AppServiceIPSecAuditLogs'
    enabled: false // 通常は不要なので無効化
  }
  {
    category: 'AppServicePlatformLogs'
    enabled: false // 通常は不要なので無効化
  }
]
```

### 2. サンプリングの導入

Application Insightsでサンプリングを設定して、送信されるテレメトリの量を減らします：

```bicep
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'ai-${baseName}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
    IngestionMode: 'ApplicationInsights'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    SamplingPercentage: 10 // 10%のサンプリングを設定
  }
}
```

また、アプリケーションコード内でも適切なサンプリング設定を行います。

### 3. 保持期間の短縮

LAWの保持期間を短縮してストレージコストを削減します：

```bicep
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: 'law-${baseName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 7 // 30日から7日に短縮
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}
```

### 4. Free Tierの活用 (Application Insights)

Application Insightsの無料枠（1日あたり最大5GB）を活用します。ただし、この設定はBicepで直接指定できないため、Azure Portalまたは別の方法で設定する必要があります。

### 5. データキャップの設定

LAWに対してデータキャップを設定することで、予期しない高額請求を防止します：

```bicep
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: 'law-${baseName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 7
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: 0.5 // 1日あたり0.5GBの上限を設定
    }
  }
}
```

### 6. 環境ごとに異なる設定の適用

本番環境と開発/テスト環境で異なる設定を適用します：

```bicep
param environment string = 'dev' // 'dev', 'test', 'prod'

var logRetention = environment == 'prod' ? 30 : 7
var enableDetailedLogs = environment == 'prod' ? true : false

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: 'law-${baseName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetention
    // 他の設定...
  }
}
```

### 7. 一部のサービスでのみログ収集を有効化

すべてのサービスではなく、特定の重要なサービス（バックエンドなど）でのみ詳細なログ収集を有効にすることを検討します。

## 実装手順

1. 既存のBicepファイルをバックアップ
2. Application Insightsの設定を変更（サンプリングの追加）
3. Log Analytics Workspaceの設定を変更（保持期間の短縮、データキャップの設定）
4. 診断設定を変更（不要なログカテゴリの無効化）
5. 環境変数に基づく条件付き設定の実装
6. 変更のテストと検証
7. 本番環境への適用

## コスト見積もり

| 項目 | 変更前 | 変更後 | 削減率 |
|------|--------|--------|--------|
| ログデータ量 | 約10GB/日 (推定) | 約1GB/日 | 90% |
| 保持期間 | 30日 | 7日 | 77% |
| 総コスト | $X/月 | $Y/月 | Z% |

注：実際のデータ量とコスト削減は現在の使用状況によって異なります。

## 推奨事項

1. まずは開発環境で上記の変更を適用し、アプリケーションの動作や監視に問題がないか確認する
2. ログやメトリクスが不足する場合は、適宜有効化する
3. アプリケーションのロギング設計を見直し、重要なログのみをApplication Insightsに送信するよう最適化する
4. 定期的にコストを確認し、必要に応じて設定を調整する

## 注意点

- ログやメトリクスの削減は監視機能の低下を意味するため、重要なログは必ず有効にしておく
- 本番環境の安定性に関わる設定変更は慎重に行う
- Azure Portalの「コスト分析」機能を活用して、変更前後のコストを比較・分析する
