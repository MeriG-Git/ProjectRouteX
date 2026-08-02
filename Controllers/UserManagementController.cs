using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RouteXWms.Data;
using RouteXWms.Models;
using RouteXWms.Services;

namespace RouteXWms.Controllers
{
    /// <summary>
    /// アカウント（ユーザー）管理・権限設定を行うコントローラー
    /// </summary>
    public class UserManagementController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public UserManagementController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// アカウント一覧画面を表示します。
        /// </summary>
        /// <returns>アカウント一覧ビュー</returns>
        public async Task<IActionResult> Index()
        {
            var users = await _context.Accounts.IgnoreQueryFilters().Where(a => !a.IsDeleted).ToListAsync();
            return View(users);
        }

        /// <summary>
        /// アカウントの新規登録または更新処理を行います。
        /// </summary>
        /// <param name="account">アカウント情報</param>
        /// <param name="isNew">新規作成フラグ</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Account account, bool isNew)
        {
            if (isNew)
            {
                var existing = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.AccountName == account.AccountName);
                if (existing != null)
                {
                    TempData["ErrorMessage"] = "指定されたアカウント名は既に存在します。";
                    return RedirectToAction(nameof(Index));
                }
                account.Password = PasswordHelper.HashPassword(account.Password);
                _context.Accounts.Add(account);
            }
            else
            {
                var existing = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.AccountName == account.AccountName);
                if (existing != null)
                {
                    if (!string.IsNullOrWhiteSpace(account.Password))
                    {
                        existing.Password = PasswordHelper.HashPassword(account.Password);
                    }
                    existing.Role = account.Role;
                    _context.Accounts.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 指定されたアカウントを論理削除します。
        /// </summary>
        /// <param name="accountName">削除対象アカウント名</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string accountName)
        {
            var item = await _context.Accounts.FindAsync(accountName);
            if (item != null)
            {
                _context.Accounts.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
