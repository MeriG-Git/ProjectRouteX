using System.Linq;
using System.Security.Claims;

namespace RouteXWms.Helpers
{
    /// <summary>
    /// ClaimsPrincipal に対する権限・ロール確認用の拡張メソッド群
    /// </summary>
    public static class UserAuthorizationExtensions
    {
        /// <summary>
        /// ユーザーが指定されたパーミッション権限（ClaimType = "Permission"）を保持しているか判定します。
        /// </summary>
        /// <param name="user">ClaimsPrincipalオブジェクト</param>
        /// <param name="permissionCode">パーミッションコード（例: Master:Edit, UserManagement:Manage）</param>
        /// <returns>権限を保持している場合 true</returns>
        public static bool HasPermission(this ClaimsPrincipal? user, string permissionCode)
        {
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                return false;
            }

            // SystemAdmin ロールを所持している場合はすべての権限を許可
            if (user.IsInRole("SystemAdmin") || user.HasClaim(ClaimTypes.Role, "SystemAdmin"))
            {
                return true;
            }

            return user.HasClaim(c => c.Type == "Permission" && c.Value == permissionCode);
        }

        /// <summary>
        /// ユーザーの表示名（DisplayName）を取得します。未設定の場合はアカウント名。
        /// </summary>
        /// <param name="user">ClaimsPrincipalオブジェクト</param>
        /// <returns>表示名</returns>
        public static string GetDisplayName(this ClaimsPrincipal? user)
        {
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                return "未ログイン";
            }

            var displayNameClaim = user.FindFirst("DisplayName")?.Value;
            if (!string.IsNullOrEmpty(displayNameClaim))
            {
                return displayNameClaim;
            }

            return user.Identity?.Name ?? "ログインユーザー";
        }
    }
}
