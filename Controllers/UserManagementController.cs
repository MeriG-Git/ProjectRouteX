using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RouteXWms.Data;
using RouteXWms.Filters;
using RouteXWms.Helpers;
using RouteXWms.Models;
using RouteXWms.Services;

namespace RouteXWms.Controllers
{
    /// <summary>
    /// アカウント（ユーザー）管理・権限設定を行うコントローラー
    /// </summary>
    [PermissionAuthorize("UserManagement:Manage")]
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
            var users = await _context.Accounts
                .IgnoreQueryFilters()
                .Where(a => !a.IsDeleted)
                .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
                .ToListAsync();

            var roles = await _context.Roles.IgnoreQueryFilters().Where(r => !r.IsDeleted).ToListAsync();
            ViewBag.Roles = roles;

            return View(users);
        }

        /// <summary>
        /// アカウントの新規登録または更新処理を行います。
        /// </summary>
        /// <param name="account">アカウント情報</param>
        /// <param name="selectedRoleIds">割り当てるロールID一覧</param>
        /// <param name="isNew">新規作成フラグ</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Account account, List<int> selectedRoleIds, bool isNew)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (isNew)
                    {
                        var existing = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.AccountName == account.AccountName);
                        if (existing != null)
                        {
                            TempData["ErrorMessage"] = "指定されたアカウント名は既に存在します。";
                            return;
                        }
                        account.Password = PasswordHelper.HashPassword(account.Password);
                        account.IsActive = true;
                        _context.Accounts.Add(account);
                        await _context.SaveChangesAsync();
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
                            existing.DisplayName = account.DisplayName;
                            existing.IsActive = account.IsActive;
                            existing.Role = account.Role;
                            _context.Accounts.Update(existing);
                            await _context.SaveChangesAsync();
                        }
                    }

                    // ロール割り当ての更新
                    var targetAccountName = isNew ? account.AccountName : account.AccountName;
                    var oldRoleMaps = await _context.AccountRoles.Where(ar => ar.AccountName == targetAccountName).ToListAsync();
                    _context.AccountRoles.RemoveRange(oldRoleMaps);
                    await _context.SaveChangesAsync();

                    if (selectedRoleIds != null && selectedRoleIds.Any())
                    {
                        foreach (var roleId in selectedRoleIds)
                        {
                            _context.AccountRoles.Add(new AccountRole
                            {
                                AccountName = targetAccountName,
                                RoleId = roleId
                            });
                        }
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                    TempData["SuccessMessage"] = $"アカウント '{targetAccountName}' の情報を更新しました。";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"保存エラー: {ErrorHelper.ToUserFriendlyMessage(ex)}";
                }
            });

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
                TempData["SuccessMessage"] = $"アカウント '{accountName}' を削除しました。";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
