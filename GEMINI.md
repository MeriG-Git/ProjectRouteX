# RouteXWms (ヤマキ物産特化型WMS) プロジェクト知識ベース (GEMINI.md)

本ファイルは、複数PC間およびAIエージェント間でプロジェクトの前提知識・決定事項を共有・維持するためのナレッジ同期ファイルです。

---

## 1. プロジェクト概要

- **名称**: RouteXWms (YK特化WMS開発)
- **目的**: ヤマキ物産様に特化した倉庫管理システム（WMS）の開発。入荷、出荷、在庫管理、棚卸、マスタ管理等の業務をデジタル化・効率化する。
- **リポジトリ**: `https://github.com/MeriG-Git/ProjectRouteX.git`

---

## 2. 技術スタック

- **バックエンド / Web**: .NET 9.0 (ASP.NET Core MVC)
- **ORM / DBアクセス**: Entity Framework Core 9.0
- **データベース**:
  - 開発・検証用: SQLite (`RouteXWms.db`)
  - 本番・クラウド移行用: Azure SQL Database / SQL Server（移行ガイド `docs/azure_database_migration_guide.md`）
- **主要ライブラリ**:
  - `MiniExcel` (v1.44.1) : Excel入出力処理
  - `Microsoft.EntityFrameworkCore.Sqlite` / `SqlServer`
- **スクリプト・ツール**:
  - Python (`generate_presentation.py`, `generate_csv.py`) : プレゼン資料作成・ダミーデータ生成用

---

## 3. 設計ルール & ガイドライン

1. **言語設定**:
   - 会話・回答、コードコメント、ドキュメント作成はすべて**日本語**を標準とする。
2. **データベース運用の原則**:
   - 開発初期・ローカルテストでは SQLite を使用。
   - スキーマ変更時は Entity Framework Core Migration を活用し、将来の Azure SQL 移行に備えた設計を維持する。
3. **セキュリティ & 権限**:
   - テスト用ログイン機能および認証フィルター (`Filters/`) を搭載。

---

## 4. ナレッジファイルの運用指示（AIエージェントへの自動更新指示）

> **【重要：AIエージェント向け指示文】**
> 今後、開発中に「新しい機能の追加」「アーキテクチャの変更」「技術スタックの追加」「重要な決定事項」が発生した場合は、**AIエージェント自身でこの `GEMINI.md` を最新状態に自律更新してください**。
> 常に最新の設計思想と決定事項が本ファイルに同期されている状態を保ちます。

---

## 5. 開発ロードマップ & 決定事項履歴

- **2026-08-02**:
  - リポジトリ初期化および `GEMINI.md` / `.gitignore` の整備実施。
  - GitHub リモート (`https://github.com/MeriG-Git/ProjectRouteX.git`) との連携完了。
  - Azure SQL 移行手順書 (`docs/azure_database_migration_guide.md`) 策定済み。
