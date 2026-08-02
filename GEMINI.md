# ProjectRouteX (ヤマキ物産特化型WMS) プロジェクト知識ベース (GEMINI.md)

本ファイルは、複数PC間およびAIエージェント間でプロジェクトの前提知識・アーキテクチャ・決定事項を同期・維持するための共有ナレッジベースです。

---

## 1. プロジェクト概要

- **プロジェクト名**: ProjectRouteX (ヤマキ物産特化型WMS)
- **フォルダ名**: `ProjectRouteX`
- **目的**: ヤマキ物産様に特化した倉庫管理システム（WMS）の開発。入荷、出荷、在庫管理、棚卸、マスタ管理等の業務をデジタル化・効率化する。
- **リポジトリ**: `https://github.com/MeriG-Git/ProjectRouteX.git`

---

## 2. 技術スタック & RDBMS 運用方針

- **バックエンド / Web**: .NET 9.0 (ASP.NET Core MVC)
- **ORM / DBアクセス**: Entity Framework Core 9.0 (SQL Server プロバイダー)
- **【必須】RDBMS (データベース)**: **Microsoft SQL Server (必須・厳守)**
  - **メインPC (DBサーバー)**: ホスト名 `subPC` / IP `192.168.40.7`
  - **データベース名**: `RouteXWmsDb`
  - **接続方式**: Windows統合認証 (`Trusted_Connection=True;TrustServerCertificate=True;`) または SQL Server 認証
  - **原則**: **SQLite への切り替えやフォールバックは禁止。全PCから SQL Server に接続を統一すること。**
- **主要ライブラリ**:
  - `MiniExcel` (v1.44.1) : Excel入出力処理
  - `Microsoft.EntityFrameworkCore.SqlServer`

---

## 3. 設計ルール & 開発ガイドライン

1. **言語設定**:
   - 会話・回答、コードコメント、ドキュメント作成はすべて**日本語**を標準とする。
2. **データベース接続ルール (SQL Server 必須)**:
   - `appsettings.json` の `DatabaseProvider` は常に `"SqlServer"` とする。
   - 他PCからLAN経由でメインPC(`subPC`)のDBに接続する場合は、接続文字列 `DefaultConnection` または `LanSqlServerConnection` の Server に `subPC` または `192.168.40.7` を指定する。
   - アプリ初回起動時に `DbInitializer.Initialize()` が自動実行され、`RouteXWmsDb` が未作成の場合はテーブルおよび初期マスターデータ（管理者ユーザー含む）が自動生成される。
3. **セキュリティ & 権限**:
   - テスト用ログイン機能および認証フィルター (`Filters/AuthFilter.cs`) を搭載。

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
  - プロジェクト名およびフォルダ名を `ProjectRouteX` に正式変更。
  - **RDBMS を SQL Server (`subPC` / `RouteXWmsDb`) に完全統一**。SQLite へのフォールバックを禁止し、SQL Server 接続を必須化。
