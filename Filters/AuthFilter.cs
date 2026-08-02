using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RouteXWms.Filters
{
    /// <summary>
    /// アクション実行前の認証チェックを行うアクションフィルター
    /// 未ログインユーザーのアクセスを制限し、ログイン画面へリダイレクトします。
    /// </summary>
    public class AuthFilter : ActionFilterAttribute
    {
        /// <summary>
        /// アクションメソッドの実行前に呼び出され、セッション状態をチェックします。
        /// </summary>
        /// <param name="context">アクション実行コンテキスト</param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // コントローラー名の取得
            var controllerName = context.RouteData.Values["controller"]?.ToString();
            
            // ログイン関連のAccountControllerへのアクセスは認証チェックを除外
            if (controllerName != null && controllerName.Equals("Account", StringComparison.OrdinalIgnoreCase))
            {
                base.OnActionExecuting(context);
                return;
            }

            // セッションからログインユーザー情報を取得
            var accountName = context.HttpContext.Session.GetString("AccountName");
            
            // 未ログインの場合はログイン画面（Account/Login）へリダイレクト
            if (string.IsNullOrEmpty(accountName))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
