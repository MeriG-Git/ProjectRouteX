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

namespace RouteXWms.Controllers
{
    /// <summary>
    /// ロールおよび権限（パーミッション）設定を管理するコントローラー
    /// </summary>
    [PermissionAuthorize("UserManagement:Manage")]
    public class RoleManagementController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public RoleManagementController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// ロール一覧画面を表示します。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var roles = await _context.Roles.IgnoreQueryFilters().Where(r => !r.IsDeleted).ToListAsync();
            var rolePermissions = await _context.RolePermissions.ToListAsync();
            var permissions = await _context.Permissions.IgnoreQueryFilters().Where(p => !p.IsDeleted).ToListAsync();

            ViewBag.RolePermissions = rolePermissions;
            ViewBag.Permissions = permissions;

            return View(roles);
        }

        /// <summary>
        /// ロール新規作成処理を実行します。
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string roleCode, string roleName, string? description)
        {
            if (string.IsNullOrWhiteSpace(roleCode) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = "ロールコードとロール名は必須項目です。";
                return RedirectToAction(nameof(Index));
            }

            var existing = await _context.Roles.FirstOrDefaultAsync(r => r.RoleCode == roleCode);
            if (existing != null)
            {
                TempData["ErrorMessage"] = $"ロールコード '{roleCode}' は既に存在します。";
                return RedirectToAction(nameof(Index));
            }

            var role = new Role
            {
                RoleCode = roleCode.Trim(),
                RoleName = roleName.Trim(),
                Description = description?.Trim()
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"ロール '{role.RoleName}' を作成しました。";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// ロールに対する権限（パーミッション）割り当ての編集画面を表示します。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EditPermissions(int id)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == id);
            if (role == null)
            {
                return NotFound();
            }

            var allPermissions = await _context.Permissions.IgnoreQueryFilters().Where(p => !p.IsDeleted).ToListAsync();
            var assignedPermissionIds = await _context.RolePermissions
                .Where(rp => rp.RoleId == id)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            ViewBag.Role = role;
            ViewBag.AssignedPermissionIds = assignedPermissionIds;

            return View(allPermissions);
        }

        /// <summary>
        /// ロールに対する権限（パーミッション）割り当てを保存・更新します。
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePermissions(int roleId, List<int> selectedPermissionIds)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId);
            if (role == null)
            {
                return NotFound();
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 既存のパーミッション割り当てを削除
                    var oldPermissions = await _context.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();
                    _context.RolePermissions.RemoveRange(oldPermissions);
                    await _context.SaveChangesAsync();

                    // 新しいパーミッション割当を追加
                    if (selectedPermissionIds != null && selectedPermissionIds.Any())
                    {
                        foreach (var permId in selectedPermissionIds)
                        {
                            _context.RolePermissions.Add(new RolePermission
                            {
                                RoleId = roleId,
                                PermissionId = permId
                            });
                        }
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                    TempData["SuccessMessage"] = $"ロール '{role.RoleName}' の権限設定を更新しました。";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"権限更新エラー: {ErrorHelper.ToUserFriendlyMessage(ex)}";
                }
            });

            return RedirectToAction(nameof(Index));
        }
    }
}
