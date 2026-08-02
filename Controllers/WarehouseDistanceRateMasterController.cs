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
    /// 倉庫距離掛率（倉庫×運賃表マッピング）マスターのCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class WarehouseDistanceRateMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public WarehouseDistanceRateMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 倉庫距離掛率マッピング一覧画面を表示します。
        /// </summary>
        /// <param name="searchName">倉庫名・運賃表名検索キーワード</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(string? searchName, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_WarehouseDistanceRate";
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

            var query = _context.WarehouseDistanceRates
                .Include(w => w.Warehouse)
                .Include(w => w.FreightTable)
                .ThenInclude(d => d!.Carrier)
                .IgnoreQueryFilters()
                .AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(w => !w.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(w => w.Warehouse!.WarehouseName.Contains(searchName) || w.FreightTable!.RateName.Contains(searchName));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(w => w.Warehouse!.WarehouseName)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
            ViewBag.FreightTables = await _context.FreightTables.Include(d => d.Carrier).OrderBy(d => d.RateName).ToListAsync();
            ViewBag.SearchName = searchName;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 倉庫距離掛率マッピングを保存（新規追加または更新）します。
        /// </summary>
        /// <param name="warehouseId">倉庫ID</param>
        /// <param name="freightTableId">運賃表ID</param>
        /// <param name="originalWarehouseId">変更前の倉庫ID</param>
        /// <param name="originalFreightTableId">変更前の運賃表ID</param>
        /// <param name="isNew">新規追加フラグ</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Guid warehouseId, Guid freightTableId, Guid originalWarehouseId, Guid originalFreightTableId, bool isNew)
        {
            if (isNew)
            {
                var existing = await _context.WarehouseDistanceRates.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId && w.FreightTableId == freightTableId);

                if (existing != null)
                {
                    existing.IsDeleted = false;
                    _context.WarehouseDistanceRates.Update(existing);
                }
                else
                {
                    var newMapping = new WarehouseDistanceRate
                    {
                        WarehouseId = warehouseId,
                        FreightTableId = freightTableId
                    };
                    _context.WarehouseDistanceRates.Add(newMapping);
                }
            }
            else
            {
                var original = await _context.WarehouseDistanceRates.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(w => w.WarehouseId == originalWarehouseId && w.FreightTableId == originalFreightTableId);

                if (original != null)
                {
                    _context.WarehouseDistanceRates.Remove(original);
                }

                var existingNew = await _context.WarehouseDistanceRates.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId && w.FreightTableId == freightTableId);

                if (existingNew != null)
                {
                    existingNew.IsDeleted = false;
                    _context.WarehouseDistanceRates.Update(existingNew);
                }
                else
                {
                    var newMapping = new WarehouseDistanceRate
                    {
                        WarehouseId = warehouseId,
                        FreightTableId = freightTableId
                    };
                    _context.WarehouseDistanceRates.Add(newMapping);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 倉庫距離掛率マッピングを論理削除します。
        /// </summary>
        /// <param name="warehouseId">倉庫ID</param>
        /// <param name="freightTableId">運賃表ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid warehouseId, Guid freightTableId)
        {
            var item = await _context.WarehouseDistanceRates.IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId && w.FreightTableId == freightTableId);
            if (item != null)
            {
                _context.WarehouseDistanceRates.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除されたマッピングを復元します。
        /// </summary>
        /// <param name="warehouseId">倉庫ID</param>
        /// <param name="freightTableId">運賃表ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid warehouseId, Guid freightTableId)
        {
            var item = await _context.WarehouseDistanceRates.IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId && w.FreightTableId == freightTableId);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数のマッピングを一括削除します。
        /// </summary>
        /// <param name="compositeIds">複合キー（倉庫ID_運賃表ID）配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(string[] compositeIds)
        {
            if (compositeIds != null && compositeIds.Length > 0)
            {
                foreach (var cid in compositeIds)
                {
                    var parts = cid.Split('_');
                    if (parts.Length == 2 && Guid.TryParse(parts[0], out var whId) && Guid.TryParse(parts[1], out var rateId))
                    {
                        var item = await _context.WarehouseDistanceRates.IgnoreQueryFilters()
                            .FirstOrDefaultAsync(w => w.WarehouseId == whId && w.FreightTableId == rateId);
                        if (item != null)
                        {
                            _context.WarehouseDistanceRates.Remove(item);
                        }
                    }
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全マッピングデータをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.WarehouseDistanceRates
                .ToListAsync();

            var headers = new[] { "倉庫ID", "運賃表ID" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, w => new[]
            {
                w.WarehouseId.ToString(),
                w.FreightTableId.ToString()
            });

            return File(bytes, "text/csv; charset=utf-8", "m_warehouse_distance_rate.csv");
        }

        /// <summary>
        /// CSVファイルからマッピングを一括インポート（追加・更新）します。
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var rows = await CsvService.ReadCsvAsync(csvFile);
                for (int i = 1; i < rows.Count; i++)
                {
                    var r = rows[i];
                    if (r.Length < 2) continue;

                    string warehouseIdStr = r[0];
                    string freightTableIdStr = r[1];

                    if (!Guid.TryParse(warehouseIdStr, out var warehouseId))
                    {
                        throw new Exception($"{i + 1}行目: 倉庫IDのフォーマットが不正です。");
                    }
                    if (!Guid.TryParse(freightTableIdStr, out var freightTableId))
                    {
                        throw new Exception($"{i + 1}行目: 運賃表IDのフォーマットが不正です。");
                    }

                    var warehouseExists = await _context.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId);
                    if (!warehouseExists)
                    {
                        throw new Exception($"{i + 1}行目: 指定された倉庫ID ({warehouseIdStr}) がマスタに登録されていません。");
                    }

                    var rateExists = await _context.FreightTables.AnyAsync(d => d.FreightTableId == freightTableId);
                    if (!rateExists)
                    {
                        throw new Exception($"{i + 1}行目: 指定された運賃表ID ({freightTableIdStr}) がマスタに登録されていません。");
                    }

                    var existing = await _context.WarehouseDistanceRates.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId && w.FreightTableId == freightTableId);

                    if (existing == null)
                    {
                        if (!createIfNotFound)
                        {
                            throw new Exception($"{i + 1}行目: 指定されたマッピング (倉庫ID: {warehouseIdStr}, 料金表ID: {freightTableIdStr}) は存在しません。");
                        }
                        var newMapping = new WarehouseDistanceRate
                        {
                            WarehouseId = warehouseId,
                            FreightTableId = freightTableId
                        };
                        _context.WarehouseDistanceRates.Add(newMapping);
                    }
                    else
                    {
                        existing.IsDeleted = false;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                TempData["SuccessMessage"] = "CSVのインポートが完了しました。";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var detail = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                TempData["ErrorMessage"] = $"インポートエラー: {detail}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
