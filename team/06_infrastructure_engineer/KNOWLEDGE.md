# ナレッジベース: インフラ設計者

## 📌 インフラ設計 & ネットワーク構成ナレッジ

### 1. 構築完了実績
- **メインPC (`subPC`) SQL Server 2025**:
  - `SuperSocketNetLib\Tcp` ポート 1433 固定設定完了。
  - Windows ファイアウォール `SQL Server (TCP 1433)` 規則作成完了 (Profile = Any, Action = Allow)。
  - `Restart-Service MSSQLSERVER` 実施済み。全外部PCからの疎通100%開通済み。

### 2. Azure 移行計画
- `docs/azure_database_migration_guide.md` に従い、将来的な Azure SQL Database への移行スクリプトを準備済み。
