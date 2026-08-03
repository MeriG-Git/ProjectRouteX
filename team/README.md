# ProjectRouteX (ヤマキ物産特化型WMS) マルチエージェントチーム ガイド

本ディレクトリは、ProjectRouteX（ヤマキ物産特化型WMS）開発プロジェクトにおいて、9名のスペシャリスト・エージェントが協調し、透明性の高い情報共有とチケット管理のもとで高品質なシステムを迅速に構築・運用するための全体構造および運用ガイドラインを定義します。

---

## 1. 専門エージェント構成一覧

| フォルダ | エージェント名 | 主な役割・責務 |
| :--- | :--- | :--- |
| [`01_project_manager/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/01_project_manager/ROLE.md) | **プロジェクトマネージャー** | 本プロジェクトの統括管理者。各エージェントへの作業指示・進捗管理・チケット割り振りを担当。 |
| [`02_3pl_analyst/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/02_3pl_analyst/ROLE.md) | **3PL業界アナリスト** | 3PL業界に精通。業界観点からの要件取りまとめ、必要機能・波動対応・荷主別業務の洗い出しを実施。 |
| [`03_wms_specialist/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/03_wms_specialist/ROLE.md) | **WMSスペシャリスト** | 各種WMSに精通。他社WMSとの差別化提案、機能設計（入荷・出荷・在庫・棚卸・請求等）を担当。 |
| [`04_software_architect/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/04_software_architect/ROLE.md) | **開発アーキテクチャー** | OOP/DDDに精通。汎用性・可用性・人間が読みやすいコード/日本語コメントを重視した設計・実装を担当。 |
| [`05_database_analyst/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/05_database_analyst/ROLE.md) | **データーベースアナリスト** | RDBMS(SQL Server)・データモデリングの最適化。拡張性・整合性・大量データパフォーマンス設計を担当。 |
| [`06_infrastructure_engineer/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/06_infrastructure_engineer/ROLE.md) | **インフラ設計者** | Azureクラウド環境およびローカル開発環境（SQL Server / LAN / IIS / .NET）のインフラ・ネットワーク設計を担当。 |
| [`07_ui_designer/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/07_ui_designer/ROLE.md) | **UIデザイナー** | UI/UX・Webデザインのスペシャリスト。最新のモダンデザイン、レスポンシブ、現場使いやすさを担当。 |
| [`08_security_administrator/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/08_security_administrator/ROLE.md) | **セキュリティ管理者** | SaaSアプリに必要なセキュリティ（認可/認証/RBAC/暗号化/監査ログ/最新セキュリティ対策）を担当。 |
| [`09_test_engineer/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/09_test_engineer/ROLE.md) | **テスト設計者** | 要件・機能・システム設計に対するテスト設計の作成と、単体/結合/自動テストの実行・維持を担当。 |

---

## 2. チケット管理システム ([`tickets/BOARD.md`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/tickets/BOARD.md))

プロジェクトの全タスクは [`team/tickets/`](file:///c:/011_%E9%96%8B%E7%99%BA/ProjectRouteX/team/tickets/) 配下のチケットで管理されます。

- **`tickets/BOARD.md`**: かんばんボード（Backlog, In Progress, In Review, Done）
- **`tickets/TICK-xxx.md`**: 個別タスクチケット（担当エージェント、概要、作業内容、成果物、進捗率）

---

## 3. 情報共有・知識蓄積ルール（隠し事ゼロの原則）

1. **オープンナレッジの原則**:
   - 各エージェントは発見・分析・設計・修正を行った際、必ず自身のフォルダ内の `KNOWLEDGE.md` に知見を更新・蓄積します。
   - 他のエージェントの `KNOWLEDGE.md` や `ROLE.md` を自由に参照し、知識の重複や矛盾を防ぎます。
2. **プロジェクトマネージャーによる定期進捗評価**:
   - プロジェクトマネージャーが `BOARD.md` を定期的に更新し、チーム全体のボトルネックを解消します。
