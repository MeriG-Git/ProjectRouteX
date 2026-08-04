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
                                throw new Exception($"{i + 1}行目: 集荷エリアIDのフォーマットが不正です。({idStr})");
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
        /// SSE ストリーミングを用いて集荷エリアマスターをリアルタイム進捗表示付きで高パフォーマンスに一括インポートします。
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task ImportCsvStream(
            IFormFile csvFile, 
            bool createIfNotFound = false, 
            Guid? defaultShipperId = null, 
            Guid? defaultShippingClassId = null, 
            Guid? defaultWarehouseId = null)
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

                // 事前検証（DBトランザクション開始前）
                List<string> missingKeys = new List<string>();

                bool checkShipper = !defaultShipperId.HasValue || defaultShipperId.Value == Guid.Empty;
                bool checkShippingClass = !defaultShippingClassId.HasValue || defaultShippingClassId.Value == Guid.Empty;
                bool checkWarehouse = !defaultWarehouseId.HasValue || defaultWarehouseId.Value == Guid.Empty;

                bool needShipper = false, needShippingClass = false, needWarehouse = false;

                for (int i = 1; i < rows.Count; i++)
                {
                    var r = rows[i];
                    if (r.Length < 1) continue;

                    string shipperIdStr = r.Length > 1 ? (r[1] ?? "").Trim() : "";
                    string classIdStr = r.Length > 2 ? (r[2] ?? "").Trim() : "";
                    string whIdStr = r.Length > 3 ? (r[3] ?? "").Trim() : "";

                    if (checkShipper && !Guid.TryParse(shipperIdStr, out _)) needShipper = true;
                    if (checkShippingClass && !Guid.TryParse(classIdStr, out _)) needShippingClass = true;
                    if (checkWarehouse && !Guid.TryParse(whIdStr, out _)) needWarehouse = true;
                }

                if (needShipper) missingKeys.Add("shipper");
                if (needShippingClass) missingKeys.Add("shippingClass");
                if (needWarehouse) missingKeys.Add("warehouse");

                if (missingKeys.Count > 0)
                {
                    await sendProgressAsync(new { status = "need_selection", missing = missingKeys, message = "未指定の参照マスターが検出されました。適用するマスターを選択してください。" });
                    return;
                }

                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    var existingMap = await _context.CollectionAreas.IgnoreQueryFilters().ToDictionaryAsync(ca => ca.AreaId);

                    int total = rows.Count - 1;
                    await sendProgressAsync(new { status = "start", current = 0, total = total, currentKey = "" });

                    int processedCount = 0;
                    int batchSize = 1000;

                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 1) continue;

                        string idStr = (r[0] ?? "").Trim();
                        string shipperIdStr = r.Length > 1 ? (r[1] ?? "").Trim() : "";
                        string classIdStr = r.Length > 2 ? (r[2] ?? "").Trim() : "";
                        string whIdStr = r.Length > 3 ? (r[3] ?? "").Trim() : "";
                        string name = r.Length > 4 ? (r[4] ?? "").Trim() : (r.Length > 1 && !Guid.TryParse(r[1], out _) ? (r[1] ?? "").Trim() : "");

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = $"集荷エリア_{i}";
                        }

                        // デフォルトフォールバック適用
                        if (string.IsNullOrWhiteSpace(shipperIdStr) && defaultShipperId.HasValue && defaultShipperId.Value != Guid.Empty)
                        {
                            shipperIdStr = defaultShipperId.Value.ToString();
                        }
                        if (string.IsNullOrWhiteSpace(classIdStr) && defaultShippingClassId.HasValue && defaultShippingClassId.Value != Guid.Empty)
                        {
                            classIdStr = defaultShippingClassId.Value.ToString();
                        }
                        if (string.IsNullOrWhiteSpace(whIdStr) && defaultWarehouseId.HasValue && defaultWarehouseId.Value != Guid.Empty)
                        {
                            whIdStr = defaultWarehouseId.Value.ToString();
                        }

                        if (!Guid.TryParse(shipperIdStr, out var shipperId))
                        {
                            throw new Exception($"{i + 1}行目: 荷主を選択するか、CSV内に有効な荷主IDを指定してください。");
                        }
                        if (!Guid.TryParse(classIdStr, out var shippingClassId))
                        {
                            throw new Exception($"{i + 1}行目: 出荷区分を選択するか、CSV内に有効な出荷区分IDを指定してください。");
                        }
                        if (!Guid.TryParse(whIdStr, out var warehouseId))
                        {
                            throw new Exception($"{i + 1}行目: 倉庫を選択するか、CSV内に有効な倉庫IDを指定してください。");
                        }

                        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var id))
                        {
                            id = Guid.NewGuid();
                            var newCa = new CollectionArea
                            {
                                AreaId = id,
                                ShipperId = shipperId,
                                ShippingClassId = shippingClassId,
                                WarehouseId = warehouseId,
                                AreaName = name
                            };
                            _context.CollectionAreas.Add(newCa);
                        }
                        else
                        {
                            if (!existingMap.TryGetValue(id, out var existing))
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
                                    AreaName = name
                                };
                                _context.CollectionAreas.Add(existing);
                                existingMap[id] = existing;
                            }
                            else
                            {
                                existing.ShipperId = shipperId;
                                existing.ShippingClassId = shippingClassId;
                                existing.WarehouseId = warehouseId;
                                existing.AreaName = name;
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

                    await sendProgressAsync(new { status = "completed", current = processedCount, total = total, message = $"集荷エリアマスターのインポートが完了しました。（全 {processedCount:N0} 件）" });
                });
            }
            catch (Exception ex)
            {
                await sendProgressAsync(new { status = "error", message = $"インポートエラー: {RouteXWms.Helpers.ErrorHelper.ToUserFriendlyMessage(ex)}" });
            }
        }
    }
}
