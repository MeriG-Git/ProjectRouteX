# ナレッジベース: データーベースアナリスト

## 📌 データベース設計 & SQL Server ノウハウ

### 1. 接続 & 環境確定情報
- **RDBMS**: Microsoft SQL Server 2025 Standard Developer Edition (`MSSQLSERVER`)
- **データベース名**: `RouteXWmsDb`
- **メインPC (DBサーバー)**: `subPC` (192.168.40.7:1433)
- **専用ユーザー**: `routex_user`

### 2. パフォーマンス & トランザクション設計ポリシー
- 主要検索キー（`shipper_id`, `carrier_id`, `location_id`, `jan_code`, `status`）への複合カバーリングインデックスの配置。
- 物理削除は行わず `is_deleted` ビット列による論理削除統一。
- **EF Core リトライ戦略規約**: `EnableRetryOnFailure()` 有効化環境では、`CreateExecutionStrategy().ExecuteAsync()` を介してトランザクションを実行し、接続切断時の自動再試行とデータ完全性を両立させる。
