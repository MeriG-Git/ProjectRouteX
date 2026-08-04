using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RouteXWms.Data;
using RouteXWms.Models;
using RouteXWms.Services;

namespace RouteXWms.Controllers
{
    /// <summary>
    /// アカウント認証（ログイン・ログアウト）を制御するコントローラー
    /// </summary>
    public class AccountController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public AccountController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// ログイン画面を表示します。
        /// 既にログイン済みの場合はホーム画面へリダイレクトします。
        /// </summary>
        /// <returns>ログインビュー</returns>
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View(new LoginViewModel());
        }

        /// <summary>
        /// ログイン認証処理を実行します。
        /// 成功時に Cookie 認証チケットを発行してログインします。
        /// </summary>
        /// <param name="model">ログイン入力ビューモデル</param>
        /// <returns>成功時ホーム画面、失敗時ログイン画面</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var account = await _context.Accounts
                .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
                .FirstOrDefaultAsync(a => a.AccountName == model.AccountName);

            if (account == null || !account.IsActive || !PasswordHelper.VerifyPassword(model.Password, account.Password))
            {
                model.ErrorMessage = "アカウント名またはパスワードが正しくありません。";
                return View(model);
            }

            // ユーザーに割り当てられているロールおよびパーミッションを取得
            var roleIds = account.AccountRoles.Select(ar => ar.RoleId).ToList();
            var roles = await _context.Roles.Where(r => roleIds.Contains(r.RoleId)).ToListAsync();

            var permissionIds = await _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => rp.PermissionId)
                .Distinct()
                .ToListAsync();

            var permissions = await _context.Permissions
                .Where(p => permissionIds.Contains(p.PermissionId))
                .Select(p => p.PermissionCode)
                .ToListAsync();

            // Claims の生成
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, account.AccountName),
                new Claim("DisplayName", string.IsNullOrEmpty(account.DisplayName) ? account.AccountName : account.DisplayName)
            };

            // ロール Claim の追加
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role.RoleCode));
            }

            // パーミッション Claim の追加
            foreach (var permCode in permissions)
            {
                claims.Add(new Claim("Permission", permCode));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            // Cookie 認証チケットの発行
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // 互換性のためのセッション保存
            HttpContext.Session.SetString("AccountName", account.AccountName);
            HttpContext.Session.SetInt32("Role", account.Role);

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// ログアウト処理を実行し、認証Cookieおよびセッションをクリアします。
        /// </summary>
        /// <returns>ログイン画面へのリダイレクト</returns>
        [HttpPost]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }
    }
}
