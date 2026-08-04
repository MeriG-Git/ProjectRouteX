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
5. **EF Core リトライ戦略 (`EnableRetryOnFailure`) 対応トランザクション原則【横展開・再発防止必須ルール】**:
   - `Program.cs` で SQL Server の自動リトライ戦略 (`sqlOptions.EnableRetryOnFailure()`) を有効化しているため、コントローラーやサービス等で明示的に `BeginTransactionAsync()` を直呼び出すことは**厳禁**（`SqlServerRetryingExecutionStrategy does not support user-initiated transactions` エラーが発生する）。
   - **実装必須パターン**:
     ```csharp
     var strategy = _context.Database.CreateExecutionStrategy();
     await strategy.ExecuteAsync(async () =>
     {
         using var transaction = await _context.Database.BeginTransactionAsync();
         // ... 業務処理 / SaveChangesAsync ...
         await transaction.CommitAsync();
     });
     ```
   - **横展開・再発防止ガイドライン**:
      1. **リアルタイム進捗通知（4フェーズ・件数・速度・パーセント表示）の全画面統一【最重要】**: 
         インポート処理においては、単にDB書き込み中のみ件数を表示したり、無表示で応答待ちにすることはUI設計不備・障害となる。必ず以下の **4フェーズ** をリアルタイムでユーザーへ透過的に通知すること：
         - **【1/4】ファイル読み込み中**: ファイル受領とCSV/Excel解析
         - **【2/4】件数チェック・構文検証中**: 全行数のカウントと入力型検証
         - **【3/4】DBマスター事前照合中**: メモリ内高速化のための既存キー・関連テーブル一括キャッシュロード
         - **【4/4】DB一括更新中**: 処理件数/全件数、進捗率(%)、リアルタイム処理速度(件/秒)、経過時間(秒)、処理中キー情報
      2. **パフォーマンス・バッチ処理の最適化**: 大量データ（数万〜数十万件）インポート時もI/OおよびDB負荷を抑え最高速で完了させるため、100件単位でのリアルタイム画面進捗通知、および 1,000件単位での `SaveChangesAsync` バッチコミット・メモリ内高速辞書判定パターンを全インポート処理に適用すること。
      3. **エラーメッセージ日本語マッピングの徹底**: 単一機能でエラーメッセージを改善した場合、他機能へ修正を漏らすことは横展開漏れ障害となる。全コントローラーの catch 節において英文例外メッセージをそのまま表示させず、必ず `ErrorHelper.ToUserFriendlyMessage(ex)` などの共通マッピングを通してユーザーに伝わる明確な日本語エラー表現に統一すること。
      4. **全件横断チェックの義務化**: CSV/Excel等のインポート処理や一括更新処理で類似エラー・仕様修正が発生した場合、単一の機能のみ修正して終了することを禁止する。必ずプロジェクト内の同種処理すべてを横断検索（`grep`等）し、全箇所へ修正を適用すること。
      5. **親マスター参照キーの画面デフォルト選択・自動補完・DBトランザクション前事前検証ルール**:
         他マスターのキー（`FreightTableId`, `CarrierId`, `ShipperId`, `WarehouseId`, `ShippingClassId` 等）を参照するマスターインポート処理では、インポートモーダル内に参照先キーのドロップダウン選択肢を設置し、CSV内のキー列が空白の場合に画面選択値を全行へ自動補完・設定する構造とすること。
         また、画面で指定されたデフォルトキーの存在チェックおよび全行への補完判定は、**DBトランザクション (`BeginTransactionAsync()`) を開始する前の事前検証フェーズ（フェーズ1〜3）で完了**させ、無効なキーが存在する場合はDBトランザクションを開かずに即時エラー返送すること。
      6. **マスター固有仕様の厳密な事前精査とコピペ転用禁止原則【再発防止】**:
         インポート処理の横展開や共通化を行う際、他マスターのコード（例: 輸送運賃の5〜6列ロジック）を安易に別マスター（例: 個配運賃の正規8列仕様）へコピペ転用することを厳禁とする。必ず対象マスター固有のエクスポート仕様・テーブル定義・ヘッダー構成を事前に精査し、仕様上の正規フォーマット（8列構成等）を最優先で正しくパースできるように実装・検証しなければならない。
      7. **マルチエージェント協議と参加エージェント表示の徹底【絶対ルール・最重要】**:
         仕様決定、設計提案、機能実装、インフラ変更を行う際は、必ずプロジェクト内の関連するスペシャリスト・エージェント間で協議を行い、合意の上で決定しなければならない。また、ユーザーへの回答・提案・報告時には、必ず**「協議に携わったエージェント一覧」を明示**すること。
      8. **テストデータ自動投入の絶対禁止・プログラム分離原則【最重要・厳守】**:
         テストデータやサンプルデータの生成・自動投入処理をプログラム内（`DbInitializer.cs` や起動処理、コントローラー等）に埋め込むことは**全面禁止**とする。アプリ起動時はスキーマ作成およびシステム必須アカウント/権限設定のみを行い、業務テストデータの流し込みはユーザーから明示的な指示（「テストデータを流し込んで」）があった場合のみ、独立した外部スクリプト/ツールで実行しなければならない。
      9. **動作検証データの即時クリーンアップ・削除原則【厳守】**:
         開発・機能改修・動作検証の過程で作成・投入したテスト用データ（DBレコード、検証用一時ファイル等）は、動作検証が完了した直後に必ずすべて物理削除または初期状態へクリーンアップし、環境に検証用データを取り残さないこと。

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
  - Azure SQL 移行手順書 (`docs/azure_database_migration_guide.md`) 策定済み。
  - **【構築完了】SQL Server 2025 Standard Developer Edition 導入成功 & 接続設計確定**:
    1. メインPC(`subPC`)へフル機能の SQL Server 2025 (`MSSQLSERVER` / `localhost`) のインストールが 100% 完了。
    2. データベース `RouteXWmsDb` および専用 SQL 認証ユーザー `routex_user`（パスワード: `RouteX1234!`、`CHECK_POLICY = OFF`）の作成・`db_owner` 権限付与・旧DBからの全データ移行を正常完了。
    3. TCP/IP プロトコル有効化 (`Enabled = 1`) および固定 1433 ポート (`TcpPort = "1433"`, `TcpDynamicPorts = ""`) のレジストリ設定スクリプト (`scratch/enable_tcp.ps1`) を自動検出版に修正完了。
    4. **管理者権限での TCP 1433 レジストリ適用・ファイアウォールルール作成・サービス再起動**:
       `Get-ChildItem "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server" | Where-Object { $_.PSChildName -like "MSSQL*" } | ForEach-Object { $tcp = $_.PSPath + "\MSSQLServer\SuperSocketNetLib\Tcp"; if (Test-Path $tcp) { Set-ItemProperty -Path $tcp -Name "Enabled" -Value 1; $ipAll = $tcp + "\IPAll"; if (Test-Path $ipAll) { Set-ItemProperty -Path $ipAll -Name "TcpPort" -Value "1433"; Set-ItemProperty -Path $ipAll -Name "TcpDynamicPorts" -Value "" } } }; New-NetFirewallRule -DisplayName "SQL Server (TCP 1433)" -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow -ErrorAction SilentlyContinue; Restart-Service MSSQLSERVER`
    5. **確定接続文字列**:
       - 自PC用: `Server=localhost;Database=RouteXWmsDb;Trusted_Connection=True;`
       - 他PC(外部端末)用: `Server=192.168.40.7;Database=RouteXWmsDb;User Id=routex_user;Password=RouteX1234!;TrustServerCertificate=True;MultipleActiveResultSets=true;`
- **2026-08-04**:
  - **案件機能の新規構築および「案件管理（業務トランザクション）」への位置づけ刷新**:
    1. 荷主に紐づく案件定義エンティティ `Project` (`m_project`)、案件×倉庫紐づけエンティティ `ProjectWarehouse` (`m_project_warehouse`)、荷主＋案件＋倉庫に対する料金表紐づけエンティティ `ProjectWarehouseFreightTable` (`m_project_warehouse_freight_table`) を新設。
    2. 旧倉庫×料金表直接紐づけ `WarehouseDistanceRate` を全面削除。
    3. 自動最安倉庫選定・運賃計算サービス (`CheapestWarehouseService.cs`) を「荷主＋案件＋倉庫」階層参照に刷新。
    4. **案件位置づけの最適化**: 案件は固定的マスターではなく業務運用（トランザクション）に近い性質を持つため、サイドナビゲーションの「マスター管理」から切り離し、独立した**「案件管理」**セクションへ再配置。不要な「案件コード」項目を全廃止し、「荷主名 ＋ 案件名称」をキーとするシンプルな運用フローに改修。

---

## 6. エージェント間連携リクエスト (subPC環境のAIエージェント宛て)

> **【subPC側 AIエージェントへの依頼事項】**
> 他端末のAIエージェントより、`subPC` (192.168.40.7) 上の SQL Server インスタンスに対する接続検証依頼が届いています。
> 以下の SQL Server 構成およびアカウント設定を確認・調整し、外部接続を有効化してください。
>
> 1. **データベース確認**:
>    - データベース `RouteXWmsDb` が存在するか、および既存スキーマの確認。
> 2. **混合モード認証（SQL Server 認証）の有効化**:
>    - SQL Server の認証モードを「SQL Server 認証と Windows 認証モード（混合モード）」に設定。
> 3. **ログインアカウント設定**:
>    - SQL ユーザー `merit.og@gmail.com`（パスワード: `!4K7o3n3`）を作成し、`RouteXWmsDb` に対する `db_owner` 権限を付与。
> 4. **ネットワークプロトコル・ファィアウォール許可**:
>    - SQL Server の TCP/IP プロトコル（ポート `1433`）および SQL Server Browser サービスを有効化。
> 5. **【追記リクエスト】Windows ファイアウォール TCP 1433 受信規則の作成**:
>    - SQL Server サービス側の TCP ポート 1433 設定は完了しましたが、Windows ファイアウォールで TCP 1433 ポートが拒否されているため接続タイムアウト（Error 10060）が発生します。
>    - `subPC` 上の管理者権限 PowerShell で `New-NetFirewallRule -DisplayName "SQL Server (TCP 1433)" -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow` を実行してファイアウォールを開放してください。
