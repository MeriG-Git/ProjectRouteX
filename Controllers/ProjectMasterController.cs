using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MiniExcelLibs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RouteXWms.Data;
using RouteXWms.Models;
using RouteXWms.Helpers;

namespace RouteXWms.Controllers
{
    /// <summary>
    /// 案件マスター管理コントローラー
    /// 荷主に紐づく案件の定義、案件ごとの利用倉庫設定、案件×倉庫に対する料金表設定を統合管理します。
    /// </summary>
    public class ProjectMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ProjectMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 案件マスター一覧表示
        /// </summary>
        public async Task<IActionResult> Index(Guid? shipperId, string? searchKeyword, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_Project";

            if (pageSize.HasValue && pageSize.Value > 0)
            {
                Response.Cookies.Append(cookieKey, pageSize.Value.ToString(), new CookieOptions { Expires = DateTimeOffset.Now.AddYears(1) });
            }
            else
            {
                if (Request.Cookies.TryGetValue(cookieKey, out string? cookieVal) && int.TryParse(cookieVal, out int parsedSize) && parsedSize > 0)
                {
                    pageSize = parsedSize;
                }
                else
                {
                    pageSize = 10;
                }
            }

            int currentPageSize = pageSize.Value;

            var query = _context.Projects
                .Include(p => p.Shipper)
                .Include(p => p.ProjectWarehouses)
                    .ThenInclude(pw => pw.Warehouse)
                .Include(p => p.ProjectWarehouseFreightTables)
                    .ThenInclude(pwft => pwft.FreightTable)
                .IgnoreQueryFilters()
                .AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(p => !p.IsDeleted);
            }

            if (shipperId.HasValue && shipperId.Value != Guid.Empty)
            {
                query = query.Where(p => p.ShipperId == shipperId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var kw = searchKeyword.Trim();
                query = query.Where(p => p.ProjectName.Contains(kw) || (p.Shipper != null && p.Shipper.ShipperName.Contains(kw)));
            }

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)currentPageSize);
            if (totalPages < 1) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var items = await query
                .OrderBy(p => p.Shipper != null ? p.Shipper.ShipperName : "")
                .ThenBy(p => p.ProjectName)
                .Skip((page - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToListAsync();

            ViewBag.Shippers = await _context.Shippers.OrderBy(s => s.ShipperName).ToListAsync();
            ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
            ViewBag.FreightTables = await _context.FreightTables.OrderBy(f => f.RateName).ToListAsync();
            ViewBag.SelectedShipper = shipperId;
            ViewBag.SearchKeyword = searchKeyword;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = currentPageSize;
            ViewBag.TotalCount = totalCount;

            return View(items);
        }

        /// <summary>
        /// 案件登録・編集保存処理
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Guid? projectId, Guid shipperId, string projectName, string? remarks)
        {
            try
            {
                if (shipperId == Guid.Empty) return BadRequest("荷主を選択してください。");
                if (string.IsNullOrWhiteSpace(projectName)) return BadRequest("案件名を入力してください。");

                projectName = projectName.Trim();

                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    if (!projectId.HasValue || projectId.Value == Guid.Empty)
                    {
                        // 案件名重複チェック
                        bool exists = await _context.Projects.IgnoreQueryFilters().AnyAsync(p => p.ShipperId == shipperId && p.ProjectName == projectName);
                        if (exists)
                        {
                            throw new InvalidOperationException("指定された荷主内に既に同じ案件名が存在します。");
                        }

                        var newProject = new Project
                        {
                            ProjectId = Guid.NewGuid(),
                            ShipperId = shipperId,
                            ProjectName = projectName,
                            Remarks = remarks,
                            IsDeleted = false
                        };
                        _context.Projects.Add(newProject);
                    }
                    else
                    {
                        var existing = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectId == projectId.Value);
                        if (existing == null) throw new InvalidOperationException("対象の案件が存在しません。");

                        bool exists = await _context.Projects.IgnoreQueryFilters().AnyAsync(p => p.ShipperId == shipperId && p.ProjectName == projectName && p.ProjectId != projectId.Value);
                        if (exists)
                        {
                            throw new InvalidOperationException("指定された荷主内に既に同じ案件名が存在します。");
                        }

                        existing.ShipperId = shipperId;
                        existing.ProjectName = projectName;
                        existing.Remarks = remarks;
                        existing.IsDeleted = false;
                        _context.Projects.Update(existing);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorHelper.ToUserFriendlyMessage(ex) });
            }
        }

        /// <summary>
        /// 案件利用倉庫設定の保存処理
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveWarehouses(Guid projectId, List<Guid> warehouseIds)
        {
            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    var existingPWs = await _context.ProjectWarehouses.IgnoreQueryFilters().Where(pw => pw.ProjectId == projectId).ToListAsync();
                    _context.ProjectWarehouses.RemoveRange(existingPWs);
                    await _context.SaveChangesAsync();

                    if (warehouseIds != null && warehouseIds.Any())
                    {
                        foreach (var whId in warehouseIds.Distinct())
                        {
                            _context.ProjectWarehouses.Add(new ProjectWarehouse
                            {
                                ProjectId = projectId,
                                WarehouseId = whId,
                                IsDeleted = false
                            });
                        }
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                });

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorHelper.ToUserFriendlyMessage(ex) });
            }
        }

        /// <summary>
        /// 案件×倉庫に対する料金表設定の保存処理
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveFreightTables(Guid projectId, Guid warehouseId, List<Guid> freightTableIds)
        {
            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    var existingPWFTs = await _context.ProjectWarehouseFreightTables.IgnoreQueryFilters()
                        .Where(pwft => pwft.ProjectId == projectId && pwft.WarehouseId == warehouseId)
                        .ToListAsync();

                    _context.ProjectWarehouseFreightTables.RemoveRange(existingPWFTs);
                    await _context.SaveChangesAsync();

                    if (freightTableIds != null && freightTableIds.Any())
                    {
                        foreach (var ftId in freightTableIds.Distinct())
                        {
                            _context.ProjectWarehouseFreightTables.Add(new ProjectWarehouseFreightTable
                            {
                                ProjectId = projectId,
                                WarehouseId = warehouseId,
                                FreightTableId = ftId,
                                IsDeleted = false
                            });
                        }
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                });

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorHelper.ToUserFriendlyMessage(ex) });
            }
        }

        /// <summary>
        /// 案件削除（論理削除）
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid projectId)
        {
            try
            {
                var item = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectId == projectId);
                if (item != null)
                {
                    item.IsDeleted = true;
                    await _context.SaveChangesAsync();
                }
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorHelper.ToUserFriendlyMessage(ex) });
            }
        }

        /// <summary>
        /// 案件復元処理
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid projectId)
        {
            try
            {
                var item = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectId == projectId);
                if (item != null)
                {
                    item.IsDeleted = false;
                    await _context.SaveChangesAsync();
                }
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorHelper.ToUserFriendlyMessage(ex) });
            }
        }

        /// <summary>
        /// CSV/Excel エクスポート処理
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportCsv(Guid? shipperId)
        {
            var query = _context.Projects
                .Include(p => p.Shipper)
                .Include(p => p.ProjectWarehouses).ThenInclude(pw => pw.Warehouse)
                .Include(p => p.ProjectWarehouseFreightTables).ThenInclude(pwft => pwft.FreightTable)
                .AsQueryable();

            if (shipperId.HasValue && shipperId.Value != Guid.Empty)
            {
                query = query.Where(p => p.ShipperId == shipperId.Value);
            }

            var projects = await query.OrderBy(p => p.Shipper != null ? p.Shipper.ShipperName : "").ThenBy(p => p.ProjectName).ToListAsync();

            var excelData = projects.Select(p => new
            {
                荷主名 = p.Shipper?.ShipperName ?? "",
                案件名 = p.ProjectName,
                利用倉庫数 = p.ProjectWarehouses.Count(pw => !pw.IsDeleted),
                登録料金表数 = p.ProjectWarehouseFreightTables.Count(pwft => !pwft.IsDeleted),
                備考 = p.Remarks ?? ""
            });

            var memoryStream = new MemoryStream();
            await memoryStream.SaveAsAsync(excelData);
            memoryStream.Position = 0;

            string fileName = $"ProjectMaster_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(memoryStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// リアルタイム進捗通知（4フェーズ）対応 CSV/Excel インポート処理
        /// </summary>
        [HttpPost]
        public async Task ImportCsvStream(IFormFile csvFile, Guid? defaultShipperId = null)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            Func<object, Task> sendProgressAsync = async (data) =>
            {
                string json = JsonSerializer.Serialize(data);
                byte[] bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
                await Response.Body.WriteAsync(bytes, 0, bytes.Length);
                await Response.Body.FlushAsync();
            };

            if (csvFile == null || csvFile.Length == 0)
            {
                await sendProgressAsync(new { status = "error", message = "ファイルが選択されていません。" });
                return;
            }

            try
            {
                // フェーズ1: ファイル読み込み中
                await sendProgressAsync(new { status = "progress", phase = 1, phaseText = "ファイル読み込み中", message = "ファイルを読み込み込んでいます..." });

                using var stream = csvFile.OpenReadStream();
                var rows = (await stream.QueryAsync()).ToList();

                if (!rows.Any())
                {
                    await sendProgressAsync(new { status = "error", message = "データが存在しません。" });
                    return;
                }

                // フェーズ2: 件数チェック・構文検証中
                await sendProgressAsync(new { status = "progress", phase = 2, phaseText = "件数チェック・構文検証中", message = $"全 {rows.Count} 件のフォーマットを検証しています..." });

                // フェーズ3: DBマスター事前照合中
                await sendProgressAsync(new { status = "progress", phase = 3, phaseText = "DBマスター事前照合中", message = "荷主マスターの事前照合を行っています..." });
                var shipperDict = await _context.Shippers.ToDictionaryAsync(s => s.ShipperName, s => s.ShipperId);

                // フェーズ4: DB一括更新中
                await sendProgressAsync(new { status = "progress", phase = 4, phaseText = "DB一括更新中", message = "DBトランザクション書き込み中..." });

                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    int processed = 0;
                    int total = rows.Count;
                    var startTime = DateTime.Now;

                    foreach (IDictionary<string, object> row in rows)
                    {
                        processed++;
                        var rowValues = row.Values.Select(v => v?.ToString() ?? "").ToList();
                        if (rowValues.Count < 1) continue;

                        string shipperName = rowValues[0].Trim();
                        string projectName = rowValues.Count > 1 ? rowValues[1].Trim() : "";
                        string remarks = rowValues.Count > 2 ? rowValues[2].Trim() : "";

                        if (string.IsNullOrWhiteSpace(projectName)) continue;

                        Guid shipperId = Guid.Empty;
                        if (shipperDict.ContainsKey(shipperName))
                        {
                            shipperId = shipperDict[shipperName];
                        }
                        else if (defaultShipperId.HasValue && defaultShipperId.Value != Guid.Empty)
                        {
                            shipperId = defaultShipperId.Value;
                        }
                        else if (shipperDict.Any())
                        {
                            shipperId = shipperDict.First().Value;
                        }

                        if (shipperId != Guid.Empty && !string.IsNullOrWhiteSpace(projectName))
                        {
                            var existing = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ShipperId == shipperId && p.ProjectName == projectName);
                            if (existing != null)
                            {
                                existing.Remarks = remarks;
                                existing.IsDeleted = false;
                                _context.Projects.Update(existing);
                            }
                            else
                            {
                                _context.Projects.Add(new Project
                                {
                                    ProjectId = Guid.NewGuid(),
                                    ShipperId = shipperId,
                                    ProjectName = projectName,
                                    Remarks = remarks,
                                    IsDeleted = false
                                });
                            }
                        }

                        if (processed % 50 == 0 || processed == total)
                        {
                            double elapsedSec = Math.Max(0.1, (DateTime.Now - startTime).TotalSeconds);
                            double speed = Math.Round(processed / elapsedSec, 1);
                            int percent = (int)((double)processed / total * 100);

                            await sendProgressAsync(new
                            {
                                status = "progress",
                                phase = 4,
                                phaseText = "DB一括更新中",
                                processed = processed,
                                total = total,
                                percent = percent,
                                speed = speed,
                                elapsed = Math.Round(elapsedSec, 1),
                                message = $"処理中: {projectName} ({processed}/{total}件)"
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                await sendProgressAsync(new { status = "complete", message = "インポートが正常に完了しました。" });
            }
            catch (Exception ex)
            {
                await sendProgressAsync(new { status = "error", message = ErrorHelper.ToUserFriendlyMessage(ex) });
            }
        }
    }
}
