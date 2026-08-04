# ナレッジベース: 開発アーキテクチャー

## 📌 設計原則 & コーディングガイドライン

### 1. コーディングルール
- **コメント原則**: すべてのクラス、インターフェース、主要なメソッドおよび複雑なロジックには必ず**日本語のXMLドキュメントコメント** (`/// <summary>`) を記述する。
- **アーキテクチャ階層**:
  - `Models/`: ドメインモデル・エンティティ
  - `Services/`: ドメインサービス・ビジネスロジック
  - `Controllers/`: アクションハンドラ・プレゼンテーション層
  - `Data/`: EF Core DbContext・リポジトリ

### 2. EF Core リトライ戦略（EnableRetryOnFailure）運用のトランザクション設計ルール
- SQL Server 自動リトライ有効化環境において、`BeginTransactionAsync()` を直接呼び出すユーザー主導トランザクションは EF Core の制限により不可。
- 必ず `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)` 内で明示的トランザクションまたは一連の処理を実行すること。

### 3. エラーメッセージの共通日本語マッピングルール (ErrorHelper)
- 技術的例外・英文例外メッセージ（SQL Server、EF Core、MiniExcel、型パース等）を画面にそのまま露出させることは厳禁。
- 必ず `ErrorHelper.ToUserFriendlyMessage(ex)` を経由してユーザーが直感的に判断できる明確な日本語エラーメッセージに置換して表示する。
- 一つのインポート処理でエラー表示を改修した場合、全インポートコントローラー（全マスタ・入荷・出荷）へ漏れなく横展開適用すること。

### 4. ローカル開発 & Azure共通 認証・認可アーキテクチャ方針
- ASP.NET Core 標準の Cookie / ClaimsPrincipal 認証を採用。
- 外部サービス・Azure依存はなく、ローカル環境（SQL Server + Cookie認証）で100%スタンドアロン動作可能。環境によるコード分岐や切り替えは不要。
- 将来のAzureデプロイ時も同一コードで動作し、Azure AD / Entra ID 連携時のみマルチ認証スキームを追加可能。

