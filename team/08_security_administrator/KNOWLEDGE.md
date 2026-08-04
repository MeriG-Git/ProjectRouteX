# ナレッジベース: セキュリティ管理者

## 📌 セキュリティ設計 & 認可ポリシー

### 1. セキュリティフィルター & 認証・認可方針
- **認証方式のモダン化**: 従来のセッションベース `AuthFilter.cs` から、ASP.NET Core 標準の Cookie / Claims 認証プロバイダーへ刷新。
- **ローカル/Azure共通動作**: ローカル環境（SQL Server + 暗号化Cookie）で完全自己完結して動作し、外部ネットワーク接続不要。環境によるコード切り替えは発生しない。
- **アクセス制御 (RBAC + Permission)**: ユーザー・ロール・パーミッションマトリクスにより、`[PermissionAuthorize("Master:Edit")]` 属性および Razor View レベルで強固な認可制御を実施。
- **データベースセキュリティ**: SQL 認証ユーザー `routex_user` に対する `db_owner` 権限運用と `TrustServerCertificate=True` の暗号化通信。
