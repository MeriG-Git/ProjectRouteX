using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RouteXWms.Helpers;

namespace RouteXWms.Filters
{
    /// <summary>
    /// パーミッションコードに基づくアクセス認可属性
    /// [PermissionAuthorize("Master:Edit")] のようにコントローラーやアクションに付与します。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class PermissionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _permissionCode;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="permissionCode">要求されるパーミッションコード</param>
        public PermissionAuthorizeAttribute(string permissionCode)
        {
            _permissionCode = permissionCode;
        }

        /// <summary>
        /// 認可検証処理を実行します。
        /// </summary>
        /// <param name="context">認可フィルターコンテキスト</param>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // 未ログインの場合はログイン画面へリダイレクト
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // 必要なパーミッション権限を保持していない場合は AccessDenied (403 Forbidden)
            if (!user.HasPermission(_permissionCode))
            {
                context.Result = new StatusCodeResult(403);
            }
        }
    }
}
