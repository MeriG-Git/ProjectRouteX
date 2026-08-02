using System;
using System.Security.Cryptography;
using System.Text;

namespace RouteXWms.Services
{
    /// <summary>
    /// パスワードのハッシュ化および検証を行うヘルパークラス
    /// SHA-256とソルトを用いて安全にパスワードを暗号化します。
    /// </summary>
    public static class PasswordHelper
    {
        /// <summary>ハッシュ生成用のソルト文字列</summary>
        private const string Salt = "_RouteXWmsSalt_2026_SecureKey";

        /// <summary>
        /// 平文パスワードにソルトを付加してSHA-256ハッシュ値を生成します。
        /// </summary>
        /// <param name="password">平文パスワード</param>
        /// <returns>Base64エンコードされたハッシュ文字列</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + Salt);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// 入力された平文パスワードとDBに保持されたハッシュ値を比較検証します。
        /// </summary>
        /// <param name="rawPassword">入力された平文パスワード</param>
        /// <param name="storedPassword">DB保持パスワード（ハッシュ値または平文）</param>
        /// <returns>一致する場合true</returns>
        public static bool VerifyPassword(string rawPassword, string storedPassword)
        {
            if (string.IsNullOrEmpty(rawPassword) || string.IsNullOrEmpty(storedPassword)) return false;

            // 移行期の旧平文パスワード直接比較フォールバック
            if (rawPassword == storedPassword) return true;

            var hashedInput = HashPassword(rawPassword);
            return hashedInput == storedPassword;
        }
    }
}
