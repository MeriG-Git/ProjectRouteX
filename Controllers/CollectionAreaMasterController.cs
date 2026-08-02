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
    /// 集荷エリアマスター（m_collection_area）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class CollectionAreaMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public CollectionAreaMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 集荷エリアマスター一覧画面を表示します。
        /// </summary>
        /// <param name="searchName">エリア名検索キーワード</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(string? searchName, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_CollectionArea";
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

            var query = _context.CollectionAreas
                .Include(c => c.Shipper)
                .Include(c => c.Warehouse)
                .Include(c => c.ShippingClass)
                .IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(c => !c.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(c => c.AreaName.Contains(searchName));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(c => c.AreaName)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.Shippers = await _context.Shippers.OrderBy(s => s.ShipperName).ToListAsync();
            ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
            ViewBag.ShippingClasses = await _context.ShippingClasses.OrderBy(s => s.ClassName).ToListAsync();
            ViewBag.SearchName = searchName;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 集荷エリアマスターを登録または更新します。
        /// </summary>
        /// <param name="area">入力モデル</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(CollectionArea area)
        {
            if (area.AreaId == Guid.Empty)
            {
                area.AreaId = Guid.NewGuid();
                _context.CollectionAreas.Add(area);
            }
            else
            {
                var existing = await _context.CollectionAreas.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.AreaId == area.AreaId);
                if (existing != null)
                {
                    existing.ShipperId = area.ShipperId;
                    existing.ShippingClassId = area.ShippingClassId;
                    existing.WarehouseId = area.WarehouseId;
                    existing.AreaName = area.AreaName;
                    existing.InvoiceType = area.InvoiceType;
                    existing.YamatoShopCode = area.YamatoShopCode;
                    existing.YamatoCustomerCode = area.YamatoCustomerCode;
                    existing.YamatoSubCode = area.YamatoSubCode;
                    existing.YamatoFreightMgmt = area.YamatoFreightMgmt;
                    existing.SenderCode = area.SenderCode;
                    _context.CollectionAreas.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 集荷エリアを論理削除します。
        /// </summary>
        /// <param name="id">エリアID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.CollectionAreas.FindAsync(id);
            if (item != null)
            {
                _context.CollectionAreas.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された集荷エリアを復元します。
        /// </summary>
        /// <param name="id">エリアID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var item = await _context.CollectionAreas.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.AreaId == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の集荷エリアを一括削除します。
        /// </summary>
        /// <param name="ids">対象エリアID配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(Guid[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.CollectionAreas.Where(c => ids.Contains(c.AreaId)).ToListAsync();
                _context.CollectionAreas.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全集荷エリアマスターをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.CollectionAreas.ToListAsync();
            var headers = new[] { "エリアID", "荷主ID", "出庫区分ID", "倉庫ID", "エリア名", "送り状種類", "ヤマト店所コード", "ヤマトお客様番号", "ヤマト分類コード", "ヤマト運賃管理区分", "ご請求先コード" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, a => new[]
            {
                a.AreaId.ToString(),
                a.ShipperId.ToString(),
                a.ShippingClassId.ToString(),
                a.WarehouseId.ToString(),
                a.AreaName,
                a.InvoiceType.ToString(),
                a.YamatoShopCode ?? "",
                a.YamatoCustomerCode ?? "",
                a.YamatoSubCode ?? "",
                a.YamatoFreightMgmt.ToString(),
                a.SenderCode ?? ""
            });

            return File(bytes, "text/csv; charset=utf-8", "m_collection_area.csv");
        }

        /// <summary>
        /// CSVファイルから集荷エリアマスターを一括インポート（追加・更新）します。
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
                    if (r.Length < 11) continue;

                    string idStr = r[0];
                    if (!Guid.TryParse(r[1], out var shipperId)) continue;
                    if (!Guid.TryParse(r[2], out var shippingClassId)) continue;
                    if (!Guid.TryParse(r[3], out var warehouseId)) continue;
                    string areaName = r[4];
                    int.TryParse(r[5], out var invType);
                    string yShop = r[6];
                    string yCust = r[7];
                    string ySub = r[8];
                    int.TryParse(r[9], out var yFreight);
                    string senderCode = r[10];

                    if (string.IsNullOrWhiteSpace(idStr))
                    {
                        var newArea = new CollectionArea
                        {
                            AreaId = Guid.NewGuid(),
                            ShipperId = shipperId,
                            ShippingClassId = shippingClassId,
                            WarehouseId = warehouseId,
                            AreaName = areaName,
                            InvoiceType = invType,
                            YamatoShopCode = yShop,
                            YamatoCustomerCode = yCust,
                            YamatoSubCode = ySub,
                            YamatoFreightMgmt = yFreight,
                            SenderCode = senderCode
                        };
                        _context.CollectionAreas.Add(newArea);
                    }
                    else
                    {
                        if (!Guid.TryParse(idStr, out var id))
                        {
                            throw new Exception($"{i + 1}行目: 集荷エリアIDのフォーマットが不正です。");
                        }
                        var existing = await _context.CollectionAreas.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.AreaId == id)
                                    ?? _context.CollectionAreas.Local.FirstOrDefault(c => c.AreaId == id);
                        if (existing == null)
                        {
                            if (!createIfNotFound)
                            {
                                throw new Exception($"{i + 1}行目: 指定された集荷エリアID ({idStr}) のレコードが存在しません。");
                            }
                            existing = new CollectionArea
                            {
                                AreaId = id,
                                ShipperId = shipperId,
                                ShippingClassId = shippingClassId,
                                WarehouseId = warehouseId,
                                AreaName = areaName,
                                InvoiceType = invType,
                                YamatoShopCode = yShop,
                                YamatoCustomerCode = yCust,
                                YamatoSubCode = ySub,
                                YamatoFreightMgmt = yFreight,
                                SenderCode = senderCode
                            };
                            _context.CollectionAreas.Add(existing);
                        }
                        else
                        {
                            existing.ShipperId = shipperId;
                            existing.ShippingClassId = shippingClassId;
                            existing.WarehouseId = warehouseId;
                            existing.AreaName = areaName;
                            existing.InvoiceType = invType;
                            existing.YamatoShopCode = yShop;
                            existing.YamatoCustomerCode = yCust;
                            existing.YamatoSubCode = ySub;
                            existing.YamatoFreightMgmt = yFreight;
                            existing.SenderCode = senderCode;
                            existing.IsDeleted = false;
                        }
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
