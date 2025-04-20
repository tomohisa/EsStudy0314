# GitHub Copilot

## EsCQRSQuestionsアプリケーションの日本語化および効率化計画

### 対象ファイル
1. Counter.razor - 削除
2. Error.razor - 日本語化
3. Home.razor - 日本語化
4. Questionair.razor - 残りの英語部分を日本語化
5. Weather.razor - 日本語化
6. NavMenu.razor - メニュー項目の日本語化と不要なページへのリンク削除

### 詳細計画

#### 1. Counter.razor
このファイルは不要なため削除します。このファイルを削除した後、関連するメニュー項目もNavMenu.razorから削除する必要があります。

#### 2. Error.razor
エラーページの全テキストを日本語化します。
- "Error."を「エラー」に変更
- "An error occurred while processing your request."を「リクエストの処理中にエラーが発生しました。」に変更
- "Request ID"を「リクエストID」に変更
- "Development Mode"を「開発モード」に変更
- その他のエラーメッセージも適切に日本語化

#### 3. Home.razor
ホームページの全テキストを日本語化します。
- ページタイトルを「リアルタイムアンケート - ホーム」に変更
- "Real-time Survey Application"を「リアルタイムアンケートアプリケーション」に変更
- ウェルカムメッセージや機能説明を日本語に変換
- "Participants"を「参加者」に変更
- "Go to Survey"ボタンを「アンケートに参加する」に変更
- "About This Application"を「このアプリケーションについて」に変更
- 特徴リストなど、その他のコンテンツをすべて日本語化

#### 4. Questionair.razor
一部はすでに日本語化されていますが、残りの英語部分を日本語化します。
- ページタイトルを「アンケート」に変更
- "Real-time Survey"を「リアルタイムアンケート」に変更
- "Survey Code"を「アンケートコード」に変更
- "Enter Survey Code"を「アンケートコードを入力」に変更
- "Go to Survey"ボタンを「アンケートに参加」に変更
- "Your Name (optional)"を「お名前（任意）」に変更
- "Submit Response"を「回答を送信」に変更
- "Submitting..."を「送信中...」に変更
- "Response Statistics"を「回答統計」に変更
- "Recent Comments"を「最近のコメント」に変更
- "No comments yet."を「まだコメントはありません。」に変更
- "Anonymous"を「匿名」に変更
- "Comment (optional)"を「コメント（任意）」に変更
- エラーメッセージやその他の英語テキストを日本語化

#### 5. Weather.razor
天気予報ページの全テキストを日本語化します。
- ページタイトルと見出しを「天気予報」に変更
- テーブルヘッダー（"Location", "Date", "Temp. (C)", "Temp. (F)", "Summary", "Actions"）を日本語化
- "Add New Weather Forecast"ボタンを「新しい天気予報を追加」に変更
- "Edit Location"ボタンを「場所を編集」に変更
- "Remove"ボタンを「削除」に変更
- モーダルダイアログのタイトルやラベルを日本語化
- 天気の概要（summaries配列の値）を日本語の天気状況に変更
- バリデーションメッセージやエラーメッセージを日本語化

#### 6. NavMenu.razor
ナビゲーションメニューの項目を日本語化し、不要なリンクを削除/変更します。
- "Real-time Survey"を「リアルタイムアンケート」に変更
- "Home"を「ホーム」に変更
- "Counter"メニュー項目を削除（Counterページ自体も削除するため）
- "Weather"を「天気予報」に変更
- "Survey"を「アンケート」に変更
- Aboutリンクを追加し、https://www.jtechs.com/japan/ にリダイレクトするように設定

### 実装アプローチ
1. まずCounterページを削除し、NavMenuからそのリンクを削除
2. Aboutリンクを追加して外部サイトへのリダイレクトを設定
3. 各ページを一つずつ日本語化していく
4. 最後に全体の整合性をチェックして、漏れがないか確認する

### 注意点
- 日本語化の際は、単なる直訳ではなく、自然な日本語表現になるよう配慮する
- ページタイトルやメタデータも含めて日本語化する
- エラーメッセージやバリデーションメッセージなど、動的に表示されるテキストも日本語化する必要がある
- 日本語フォントが正しく表示されることを確認する