using System.Linq;
using System.Threading.Tasks;
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
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("AccountName")))
            {
                return RedirectToAction("Index", "Home");
            }
            return View(new LoginViewModel());
        }

        /// <summary>
        /// ログイン認証処理を実行します。
        /// 成功時にセッションへユーザー情報を設定します。
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
                .FirstOrDefaultAsync(a => a.AccountName == model.AccountName);

            if (account == null || !PasswordHelper.VerifyPassword(model.Password, account.Password))
            {
                model.ErrorMessage = "アカウント名またはパスワードが正しくありません。";
                return View(model);
            }

            // セッションにログイン情報（アカウント名、ロール）を保存
            HttpContext.Session.SetString("AccountName", account.AccountName);
            HttpContext.Session.SetInt32("Role", account.Role);

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// ログアウト処理を実行し、セッションをクリアします。
        /// </summary>
        /// <returns>ログイン画面へのリダイレクト</returns>
        [HttpPost]
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }
    }
}
