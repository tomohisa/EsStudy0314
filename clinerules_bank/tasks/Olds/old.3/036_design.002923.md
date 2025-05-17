# GitHub Copilot

# リアルタイムアンケートアプリケーション モダンデザイン計画

## 現状分析

現在のデザインは標準的なBlazorテンプレートを使用した、古典的なスタイルとなっています：

1. **サイドバー**: 濃い青から紫へのグラデーション（ダークモード）
2. **メインコンテンツ**: 白背景のライトモード
3. **ナビゲーション**: 縦型のサイドメニュー
4. **全体的な印象**: コントラストが強く、やや古めかしい

## デザイン目標

30-40代の男性エンジニア向けに、以下の特徴を持つモダンなライトモードデザインを実現します：

1. **プロフェッショナルで洗練された印象**: クリーンでシンプルな見た目
2. **読みやすさ重視**: 適切なコントラストと余白の確保
3. **一貫性のあるライトモード**: 全体的に統一感のあるデザイン
4. **モダンな要素**: フラットデザイン、柔らかい影、穏やかな色合い

## 具体的な変更計画

### 1. カラースキーム

**現状**: 
- サイドバー: 濃い青〜紫のグラデーション
- メインコンテンツ: 白背景

**新デザイン**:
- 全体的に明るいテーマに統一
- プライマリーカラー: `#2563EB`（モダンな青）
- セカンダリーカラー: `#F3F4F6`（ライトグレー）
- アクセントカラー: `#3B82F6`（ブライトブルー）
- テキストカラー: `#1F2937`（ダークグレー）と`#6B7280`（ミディアムグレー）

### 2. レイアウト構造

**現状**:
- 縦型サイドバーとメインコンテンツの2カラムレイアウト

**新デザイン**:
- 横型のトップナビゲーションバーに変更
- コンテンツエリアを広く活用
- レスポンシブ対応の改善

### 3. ナビゲーション

**現状**:
- 左側の縦型メニュー
- アイコン+テキストのリンク

**新デザイン**:
- トップナビゲーションバー（水平メニュー）
- モバイル対応のハンバーガーメニュー
- 洗練されたアイコンセット（シンプルな線画スタイル）

### 4. タイポグラフィ

**現状**:
- デフォルトのBootstrapフォント

**新デザイン**:
- システムフォントスタック: `-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`
- 読みやすいサイズ設定（ベースフォントサイズ: 16px）
- 適切な行間とレターススペーシング

### 5. コンポーネント改善

**現状**:
- 基本的なBootstrapコンポーネント

**新デザイン**:
- カード: 柔らかな影（`box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)`）
- ボタン: フラットデザインに微細な立体感
- フォーム要素: よりクリーンでモダンなスタイル

## 変更対象ファイル詳細

### 1. MainLayout.razor
```razor
<div class="page">
    <header class="top-nav">
        <div class="container">
            <div class="logo">リアルタイムアンケート</div>
            <NavMenu />
        </div>
    </header>

    <main class="container">
        <article class="content">
            @Body
        </article>
    </main>

    <footer class="footer">
        <div class="container">
            <div class="footer-content">
                <a href="https://www.jtechs.com/japan/" target="_blank">会社概要</a>
            </div>
        </div>
    </footer>
</div>

<div id="blazor-error-ui">
    エラーが発生しました。
    <a href="" class="reload">再読み込み</a>
    <a class="dismiss">🗙</a>
</div>
```

### 2. NavMenu.razor
```razor
<nav class="navbar">
    <button title="Navigation menu" class="navbar-toggler" @onclick="ToggleNavMenu">
        <span class="navbar-toggler-icon"></span>
    </button>

    <div class="@NavMenuCssClass">
        <ul class="nav-links">
            <li class="nav-item">
                <NavLink class="nav-link" href="" Match="NavLinkMatch.All">
                    <span class="icon icon-home" aria-hidden="true"></span> ホーム
                </NavLink>
            </li>
            <li class="nav-item">
                <NavLink class="nav-link" href="questionair">
                    <span class="icon icon-survey" aria-hidden="true"></span> アンケート
                </NavLink>
            </li>
        </ul>
    </div>
</nav>

@code {
    private bool collapseNavMenu = true;

    private string? NavMenuCssClass => collapseNavMenu ? "collapse" : null;

    private void ToggleNavMenu()
    {
        collapseNavMenu = !collapseNavMenu;
    }
}
```

### 3. MainLayout.razor.css
```css
:root {
    --primary-color: #2563EB;
    --secondary-color: #F3F4F6;
    --accent-color: #3B82F6;
    --text-primary: #1F2937;
    --text-secondary: #6B7280;
    --shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
    --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
}

.page {
    position: relative;
    display: flex;
    flex-direction: column;
    min-height: 100vh;
    background-color: #FAFAFA;
}

main {
    flex: 1;
    padding: 2rem 0;
}

.container {
    width: 100%;
    max-width: 1200px;
    margin: 0 auto;
    padding: 0 1rem;
}

.top-nav {
    background-color: white;
    border-bottom: 1px solid #E5E7EB;
    box-shadow: var(--shadow-sm);
    padding: 0.75rem 0;
}

.top-nav .container {
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.logo {
    font-size: 1.25rem;
    font-weight: 600;
    color: var(--primary-color);
}

.content {
    background-color: white;
    border-radius: 0.5rem;
    box-shadow: var(--shadow-md);
    padding: 2rem;
}

.footer {
    margin-top: auto;
    padding: 1.5rem 0;
    background-color: var(--secondary-color);
    border-top: 1px solid #E5E7EB;
}

.footer-content {
    display: flex;
    justify-content: flex-end;
}

.footer-content a {
    color: var(--text-secondary);
    text-decoration: none;
    transition: color 0.2s ease;
}

.footer-content a:hover {
    color: var(--primary-color);
    text-decoration: underline;
}

#blazor-error-ui {
    background: #FEF2F2;
    color: #991B1B;
    bottom: 0;
    box-shadow: 0 -1px 2px rgba(0, 0, 0, 0.1);
    display: none;
    left: 0;
    padding: 0.75rem 1.25rem;
    position: fixed;
    width: 100%;
    z-index: 1000;
    border-top: 1px solid #FCA5A5;
}

#blazor-error-ui .dismiss {
    cursor: pointer;
    position: absolute;
    right: 0.75rem;
    top: 0.5rem;
    color: #991B1B;
}

@media (max-width: 640px) {
    .content {
        padding: 1.5rem;
    }
}
```

### 4. NavMenu.razor.css
```css
.navbar {
    display: flex;
    align-items: center;
}

.nav-links {
    display: flex;
    list-style: none;
    margin: 0;
    padding: 0;
}

.nav-item {
    margin-left: 1.5rem;
}

.nav-link {
    display: flex;
    align-items: center;
    color: var(--text-secondary);
    text-decoration: none;
    padding: 0.5rem 0;
    font-weight: 500;
    transition: color 0.2s ease;
}

.nav-link:hover, .nav-link.active {
    color: var(--primary-color);
}

.icon {
    display: inline-block;
    width: 1.25rem;
    height: 1.25rem;
    margin-right: 0.5rem;
    background-size: contain;
    background-repeat: no-repeat;
    background-position: center;
}

.icon-home {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpath d='M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z'%3E%3C/path%3E%3Cpolyline points='9 22 9 12 15 12 15 22'%3E%3C/polyline%3E%3C/svg%3E");
}

.icon-survey {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Ccircle cx='12' cy='12' r='10'%3E%3C/circle%3E%3Cpath d='M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3'%3E%3C/path%3E%3Cline x1='12' y1='17' x2='12.01' y2='17'%3E%3C/line%3E%3C/svg%3E");
}

.navbar-toggler {
    display: none;
    background: none;
    border: none;
    cursor: pointer;
}

.navbar-toggler-icon {
    display: block;
    width: 1.5rem;
    height: 1.5rem;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cline x1='3' y1='12' x2='21' y2='12'%3E%3C/line%3E%3Cline x1='3' y1='6' x2='21' y2='6'%3E%3C/line%3E%3Cline x1='3' y1='18' x2='21' y2='18'%3E%3C/line%3E%3C/svg%3E");
    background-size: contain;
    background-repeat: no-repeat;
    background-position: center;
}

@media (max-width: 768px) {
    .navbar {
        flex-direction: column;
        align-items: flex-start;
    }
    
    .navbar-toggler {
        display: block;
        align-self: flex-end;
    }
    
    .collapse {
        display: none;
    }
    
    .nav-links {
        flex-direction: column;
        width: 100%;
        margin-top: 1rem;
    }
    
    .nav-item {
        margin-left: 0;
        margin-bottom: 0.5rem;
    }
}
```

### 5. App.razor (cssのリンク部分の追加)

HTMLの`<head>`セクション内に追加するスタイルシート：

```html
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
<style>
    body {
        font-family: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
        color: #1F2937;
        line-height: 1.5;
        font-size: 16px;
    }
</style>
```

## 実装ステップ

1. まず、MainLayout.razor.cssとNavMenu.razor.cssのスタイルシートを更新
2. 次に、MainLayout.razorとNavMenu.razorの構造を変更
3. 最後に、App.razorにフォントの追加とベースのスタイルを設定

この計画により、エンジニア向けの洗練されたモダンなデザインになり、使いやすさと視認性が向上します。🎨