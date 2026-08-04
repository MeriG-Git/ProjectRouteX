# Ticket TICK-008: EF Core リトライ戦略 (EnableRetryOnFailure) トランザクション対応

- **担当エージェント**: 開発アーキテクチャー / データーベースアナリスト
- **優先度**: 🔥 高
- **ステータス**: ✅ Done (100%)
- **作成日**: 2026-08-03
- **完了日**: 2026-08-03

---

## 📝 目的・作業内容
SQL Server 環境において `EnableRetryOnFailure()`（接続エラー自動リトライ戦略）を有効化した場合、`_context.Database.BeginTransactionAsync()` を直接呼び出すと EF Core の仕様制限により `SqlServerRetryingExecutionStrategy does not support user-initiated transactions` 例外が発生する。

本タスクでは、該当するコントローラー・サービスでのトランザクション呼び出しを `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)` パターンへ統一・最適化する。

## 🎯 成果物 & 実施した対応
1. `team/04_software_architect/KNOWLEDGE.md` に設計ルールを追加。
2. CSVインポートおよび一括トランザクション処理におけるリトライ戦略パターンの確立。
3. `CreateExecutionStrategy()` による障害耐性とアトミック性の両立。

## 🔗 関連ナレッジ
- [開発アーキテクチャー KNOWLEDGE.md](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/04_software_architect/KNOWLEDGE.md)
- [データーベースアナリスト KNOWLEDGE.md](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/05_database_analyst/KNOWLEDGE.md)
