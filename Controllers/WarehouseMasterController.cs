using System;
using System.Linq;
using System.Text;
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
    /// 倉庫マスター（m_warehouse）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class WarehouseMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public WarehouseMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 倉庫マスター一覧画面を表示します。
        /// </summary>
        /// <param name="searchName">倉庫名検索文字列</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>倉庫マスター一覧ビュー</returns>
        public async Task<IActionResult> Index(string? searchName, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_Warehouse";
            if (pageSize.HasValue)
            {
                Response.Cookies.Append(cookieKey, pageSize.Value.ToString(), new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
            }
            else
            {
                if (Request.Cookies.TryGetValue(cookieKey, out var cookieVal) && int.TryParse(cookieVal, out var savedSize))
                {
                    pageSize = savedSize;
                }
                else
                {
                    pageSize = 10;
                }
            }
            int actualPageSize = pageSize.Value;

            var query = _context.Warehouses.IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(w => !w.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(w => w.WarehouseName.Contains(searchName));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(w => w.WarehouseName)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.SearchName = searchName;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 倉庫マスターの新規登録または更新保存を行います。
        /// </summary>
        /// <param name="warehouse">入力倉庫モデル</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Warehouse warehouse)
        {
            if (warehouse.WarehouseId == Guid.Empty)
            {
                warehouse.WarehouseId = Guid.NewGuid();
                _context.Warehouses.Add(warehouse);
            }
            else
            {
                var existing = await _context.Warehouses.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.WarehouseId == warehouse.WarehouseId);
                if (existing != null)
                {
                    existing.WarehouseName = warehouse.WarehouseName;
                    existing.ZipCode = warehouse.ZipCode;
                    existing.Address = warehouse.Address;
                    existing.Tel = warehouse.Tel;
                    _context.Warehouses.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 指定された倉庫を論理削除します。
        /// </summary>
        /// <param name="id">倉庫ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.Warehouses.FindAsync(id);
            if (item != null)
            {
                _context.Warehouses.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された倉庫を復元します。
        /// </summary>
        /// <param name="id">倉庫ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var item = await _context.Warehouses.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.WarehouseId == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の倉庫を一括で論理削除します。
        /// </summary>
        /// <param name="ids">対象倉庫ID配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(Guid[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.Warehouses.Where(w => ids.Contains(w.WarehouseId)).ToListAsync();
                _context.Warehouses.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全倉庫マスターをBOM付きUTF-8形式のCSVファイルとしてダウンロード出力します。
        /// </summary>
        /// <returns>CSVファイルレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.Warehouses.ToListAsync();
            var headers = new[] { "倉庫ID", "倉庫名", "郵便番号", "住所", "電話番号" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, w => new[]
            {
                w.WarehouseId.ToString(),
                w.WarehouseName,
                w.ZipCode,
                w.Address,
                w.Tel
            });

            return File(bytes, "text/csv; charset=utf-8", "m_warehouse.csv");
        }

        /// <summary>
        /// CSVファイルから倉庫マスターを一括インポート（追加・更新）します。
        /// </summary>
        /// <param name="csvFile">CSVファイル</param>
        /// <param name="createIfNotFound">未存在時新規作成フラグ</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCsv(IFormFile csvFile, bool createIfNotFound = false)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["ErrorMessage"] = "CSVファイルを選択してください。";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var rows = await CsvService.ReadCsvAsync(csvFile);
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 5) continue;

                        string idStr = r[0];
                        string name = r[1];
                        string zip = r[2];
                        string addr = r[3];
                        string tel = r[4];

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newWh = new Warehouse
                            {
                                WarehouseId = Guid.NewGuid(),
                                WarehouseName = name,
                                ZipCode = zip,
                                Address = addr,
                                Tel = tel
                            };
                            _context.Warehouses.Add(newWh);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 倉庫IDのフォーマットが不正です。({idStr})");
                            }
                            var existing = await _context.Warehouses.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.WarehouseId == id)
                                        ?? _context.Warehouses.Local.FirstOrDefault(w => w.WarehouseId == id);
                            if (existing == null)
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された倉庫ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new Warehouse
                                {
                                    WarehouseId = id,
                                    WarehouseName = name,
                                    ZipCode = zip,
                                    Address = addr,
                                    Tel = tel
                                };
                                _context.Warehouses.Add(existing);
                            }
                            else
                            {
                                existing.WarehouseName = name;
                                existing.ZipCode = zip;
                                existing.Address = addr;
                                existing.Tel = tel;
                                existing.IsDeleted = false;
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                TempData["SuccessMessage"] = "CSVのインポートが完了しました。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"インポートエラー: {RouteXWms.Helpers.ErrorHelper.ToUserFriendlyMessage(ex)}";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// SSE ストリーミングを用いて倉庫マスターをリアルタイム進捗表示付きで高パフォーマンスに一括インポートします。
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task ImportCsvStream(IFormFile csvFile, bool createIfNotFound = false)
        {
            Response.ContentType = "text/event-stream";
            Func<object, Task> sendProgressAsync = async (data) =>
            {
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                await Response.WriteAsync($"data: {json}\n\n");
                await Response.Body.FlushAsync();
            };

            if (csvFile == null || csvFile.Length == 0)
            {
                await sendProgressAsync(new { status = "error", message = "CSVファイルを選択してください。" });
                return;
            }

            try
            {
                var rows = await CsvService.ReadCsvAsync(csvFile);
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    var existingMap = await _context.Warehouses.IgnoreQueryFilters().ToDictionaryAsync(w => w.WarehouseId);

                    int total = rows.Count - 1;
                    await sendProgressAsync(new { status = "start", current = 0, total = total, currentKey = "" });

                    int processedCount = 0;
                    int batchSize = 1000;

                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 2) continue;

                        string idStr = (r[0] ?? "").Trim();
                        string name = (r[1] ?? "").Trim();
                        string zip = r.Length > 2 ? (r[2] ?? "").Trim() : "";
                        string addr = r.Length > 3 ? (r[3] ?? "").Trim() : "";
                        string tel = r.Length > 4 ? (r[4] ?? "").Trim() : "";

                        if (string.IsNullOrWhiteSpace(name)) continue;

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newWh = new Warehouse
                            {
                                WarehouseId = Guid.NewGuid(),
                                WarehouseName = name,
                                ZipCode = zip,
                                Address = addr,
                                Tel = tel
                            };
                            _context.Warehouses.Add(newWh);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 倉庫IDのフォーマットが不正です。({idStr})");
                            }

                            if (!existingMap.TryGetValue(id, out var existing))
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された倉庫ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new Warehouse
                                {
                                    WarehouseId = id,
                                    WarehouseName = name,
                                    ZipCode = zip,
                                    Address = addr,
                                    Tel = tel
                                };
                                _context.Warehouses.Add(existing);
                                existingMap[id] = existing;
                            }
                            else
                            {
                                existing.WarehouseName = name;
                                existing.ZipCode = zip;
                                existing.Address = addr;
                                existing.Tel = tel;
                                existing.IsDeleted = false;
                            }
                        }

                        processedCount++;

                        if (processedCount % 100 == 0 || i == rows.Count - 1)
                        {
                            await sendProgressAsync(new { status = "processing", current = processedCount, total = total, currentKey = name });
                        }

                        if (processedCount % batchSize == 0)
                        {
                            await _context.SaveChangesAsync();
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await sendProgressAsync(new { status = "completed", current = processedCount, total = total, message = $"倉庫マスターのインポートが完了しました。（全 {processedCount:N0} 件）" });
                });
            }
            catch (Exception ex)
            {
                await sendProgressAsync(new { status = "error", message = $"インポートエラー: {RouteXWms.Helpers.ErrorHelper.ToUserFriendlyMessage(ex)}" });
            }
        }
    }
}
