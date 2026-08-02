using System.ComponentModel.DataAnnotations;

namespace RouteXWms.Models
{
    /// <summary>
    /// ログイン画面用ビューモデル
    /// フォーム入力値および認証エラーメッセージを保持します。
    /// </summary>
    public class LoginViewModel
    {
        /// <summary>入力されたアカウント名</summary>
        [Required(ErrorMessage = "アカウント名を入力してください。")]
        [Display(Name = "アカウント名")]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>入力されたパスワード</summary>
        [Required(ErrorMessage = "パスワードを入力してください。")]
        [DataType(DataType.Password)]
        [Display(Name = "パスワード")]
        public string Password { get; set; } = string.Empty;

        /// <summary>画面表示用エラーメッセージ</summary>
        public string? ErrorMessage { get; set; }
    }
}
