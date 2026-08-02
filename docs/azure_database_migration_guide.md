# データベース運用 & Azure 移行ガイド

本ドキュメントでは、現状のローカルPCでの SQL Server 運用環境および、将来的に Azure（Azure SQL Database / Azure VM 上の SQL Server）へスムーズに移行・拡張するための手順と設定方法について解説します。

---

## 1. 現状の構成：このPC（ローカル環境）での SQL Server 運用

現在、本アプリケーションは **Entity Framework Core (SqlServer プロバイダー)** を使用して動作します。

### 設定ファイル (`appsettings.json`)
```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RouteXWmsDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
}
```

- **(localdb)\\mssqllocaldb**: Visual Studio / .NET SDK 標準の軽量SQL Serverインスタンスです。
- アプリケーション起動時に、`DbInitializer.Initialize(db)` が自動実行され、データベースの作成と初期マスターデータの投入が行われます。

---

## 2. 将来の構成：Azure 環境への移行手順

将来的にデータベースを Azure 上で運用する場合、ソースコードを変更することなく、**接続文字列と環境設定の変更のみ**で移行できます。

### パターン A: Azure SQL Database を使用する場合 (推奨)

#### ステップ 1: Azure 側でのリソース作成
1. Azure ポータルにログインし、**[Azure SQL]** > **[単一データベース]** を作成します。
2. サーバー作成時に「SQL 認証」を選択し、管理者ユーザー名とパスワードを設定します。
3. **[ネットワーク]** 設定で、以下を許可します：
   - [Azure サービスおよびリソースにこのサーバーへのアクセスを許可する] を有効化
   - アプリケーションを実行するPC/サーバーの パブリック IP アドレスをファイアウォール規則に追加

#### ステップ 2: 接続文字列の設定
Azure ポータルでデータベースの [接続文字列] (ADO.NET) をコピーし、`appsettings.json` または環境変数に設定します。

**`appsettings.json` の書き換え例:**
```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:YOUR-SERVER-NAME.database.windows.net,1433;Initial Catalog=RouteXWmsDb;Persist Security Info=False;User ID=YOUR-ADMIN-USER;Password=YOUR-PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

> [!TIP]
> **本番環境・クラウドデプロイ時のセキュリティ推進**
> データベースのパスワードなどの機密情報は `appsettings.json` に直接記述せず、以下の方法で注入することを強く推奨します：
> - **Azure App Service 環境変数**: `ConnectionStrings__DefaultConnection` という名前で環境変数を設定。
> - **.NET ユーザーシークレット (開発環境用)**: `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`

---

### パターン B: Azure VM (仮想マシン) 上の SQL Server を使用する場合

1. Azure VM 上で Windows Server + SQL Server を起動します。
2. SQL Server 認証を有効にし、TCP/IP ポート 1433 を有効化します。
3. Azure NSG (ネットワークセキュリティグループ) および Windows ファイアウォールで 1433 ポートの受信を許可します。
4. 接続文字列に VM のパブリック IP または FQDN を指定します。

---

## 3. アプリケーションの接続障害への強さ（レジリエンス）について

`Program.cs` では、ネットワークのゆらぎやスリープ復帰時の接続瞬断に対処するため、自動リトライオプション（`EnableRetryOnFailure`）が組み込まれています。

```csharp
options.UseSqlServer(sqlServerConn, sqlOptions =>
{
    sqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(30),
        errorNumbersToAdd: null);
});
```

により、クラウド環境特有の過渡的な接続エラー（Transient Errors）が発生しても、アプリケーションが自動的に再接続を試行します。
