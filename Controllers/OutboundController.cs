using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using RouteXWms.Data;
using RouteXWms.Models;
using RouteXWms.Services;

namespace RouteXWms.Controllers
{
    /// <summary>
    /// 出荷指示管理、出荷データ一覧・検索、最安倉庫選定、引当・確定、CSV出力等を制御するコントローラー
    /// </summary>
    public class OutboundController : Controller
    {
        private readonly WmsDbContext _context;
        private readonly CheapestWarehouseService _cheapestWarehouseService;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        /// <param name="cheapestWarehouseService">最安倉庫選定サービス</param>
        public OutboundController(WmsDbContext context, CheapestWarehouseService cheapestWarehouseService)
        {
            _context = context;
            _cheapestWarehouseService = cheapestWarehouseService;
        }

        /// <summary>
        /// 出荷データ一覧画面を表示します。
        /// </summary>
        /// <param name="groupCode">出荷指示グループコード検索条件</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの件数</param>
        /// <returns>出荷一覧ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> List(string? groupCode, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Outbound_List";
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

            var query = _context.Outbounds
                .Include(o => o.Shipper)
                .Include(o => o.Warehouse)
                .Include(o => o.Product)
                .Include(o => o.Carrier)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(groupCode))
            {
                query = query.Where(o => o.ShippingInstructionGroup.Contains(groupCode));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.GroupCode = groupCode;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 運賃・倉庫マッピング設定診断用データをJSON形式で出力します。
        /// </summary>
        /// <returns>診断データのJSONレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> CheckRates()
        {
            var carriers = await _context.Carriers.Select(c => new { c.CarrierId, c.CarrierName }).ToListAsync();
            var freightTables = await _context.FreightTables.Select(d => new { d.FreightTableId, d.RateName, d.RateTableType, d.CarrierId }).ToListAsync();
            var projectWarehouseFreightTables = await _context.ProjectWarehouseFreightTables.Select(w => new { w.ProjectId, w.WarehouseId, w.FreightTableId, w.IsDeleted }).ToListAsync();
            var shippingClasses = await _context.ShippingClasses.Select(s => new { s.ShippingClassId, s.ClassName, s.CarrierId, s.RateTableType }).ToListAsync();
            var collectionAreas = await _context.CollectionAreas.Select(a => new { a.ShipperId, a.WarehouseId, a.ShippingClassId, a.SenderCode }).ToListAsync();
            var warehouses = await _context.Warehouses.Select(w => new { w.WarehouseId, w.WarehouseName }).ToListAsync();
            
            return Json(new {
                Carriers = carriers,
                FreightTables = freightTables,
                ProjectWarehouseFreightTables = projectWarehouseFreightTables,
                ShippingClasses = shippingClasses,
                CollectionAreas = collectionAreas,
                Warehouses = warehouses
            });
        }

        /// <summary>
        /// 出荷倉庫・運賃確認画面（ステータスが「確認中」または「該当料金無し」のデータ）を表示します。
        /// </summary>
        /// <param name="groupCode">グループコード検索条件</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの件数</param>
        /// <returns>倉庫確認ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> ConfirmWarehouse(string? groupCode, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Outbound_ConfirmWarehouse";
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

            var query = _context.Outbounds
                .Include(o => o.Shipper)
                .Include(o => o.Warehouse)
                .Include(o => o.Product)
                .Include(o => o.Carrier)
                .Include(o => o.ShippingClass)
                .Where(o => o.Status == 1 || o.Status == 801);

            if (!string.IsNullOrWhiteSpace(groupCode))
            {
                query = query.Where(o => o.ShippingInstructionGroup.Contains(groupCode));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.GroupCode = groupCode;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 単一の出庫データを個別で確定（ステータスを「予定」に更新）します。
        /// </summary>
        /// <param name="id">出庫ID</param>
        /// <returns>確認画面へのリダイレクト</returns>
        [HttpPost]
        public async Task<IActionResult> ConfirmSingle(Guid id)
        {
            var record = await _context.Outbounds.FindAsync(id);
            if (record == null)
            {
                TempData["ErrorMessage"] = "対象の出庫データが見つかりません。";
                return RedirectToAction(nameof(ConfirmWarehouse));
            }

            if (record.Status != 1)
            {
                TempData["ErrorMessage"] = "ステータスが確認中以外のデータは確認済にできません。";
                return RedirectToAction(nameof(ConfirmWarehouse));
            }

            record.Status = 11;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "出荷倉庫確認を完了（予定に更新）しました。";
            return RedirectToAction(nameof(ConfirmWarehouse));
        }

        /// <summary>
        /// 選択された複数の出庫データを一括で確定（ステータスを「予定」に更新）します。
        /// </summary>
        /// <param name="ids">出庫IDリスト</param>
        /// <returns>確認画面へのリダイレクト</returns>
        [HttpPost]
        public async Task<IActionResult> ConfirmBulk(List<Guid> ids)
        {
            if (ids == null || !ids.Any())
            {
                TempData["ErrorMessage"] = "確認対象が選択されていません。";
                return RedirectToAction(nameof(ConfirmWarehouse));
            }

            var records = await _context.Outbounds
                .Where(o => ids.Contains(o.OutboundId) && o.Status == 1)
                .ToListAsync();

            if (!records.Any())
            {
                TempData["ErrorMessage"] = "該当する確認中のデータが見つかりませんでした。";
                return RedirectToAction(nameof(ConfirmWarehouse));
            }

            foreach (var record in records)
            {
                record.Status = 11;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{records.Count}件のデータをまとめて確認（予定に更新）しました。";
            return RedirectToAction(nameof(ConfirmWarehouse));
        }

        /// <summary>
        /// 現在の検索条件に該当するすべての「確認中」データを一括確定します。
        /// </summary>
        /// <param name="groupCode">グループコード検索条件</param>
        /// <returns>確認画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmAll(string? groupCode)
        {
            var query = _context.Outbounds.Where(o => o.Status == 1 && !o.IsDeleted);
            if (!string.IsNullOrWhiteSpace(groupCode))
            {
                query = query.Where(o => o.ShippingInstructionGroup.Contains(groupCode));
            }

            var targetOutbounds = await query.ToListAsync();

            if (!targetOutbounds.Any())
            {
                TempData["SuccessMessage"] = "対象となる「確認中」のデータはありませんでした。";
                return RedirectToAction(nameof(ConfirmWarehouse), new { groupCode });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    var targetIds = targetOutbounds.Select(o => o.OutboundId).ToList();

                    foreach (var outbound in targetOutbounds)
                    {
                        outbound.Status = 11;
                    }

                    var allocations = await _context.OutboundAllocations
                        .Include(a => a.Inventory)
                        .Where(a => targetIds.Contains(a.OutboundId) && !a.IsDeleted)
                        .ToListAsync();

                    foreach (var alloc in allocations)
                    {
                        alloc.Status = 11;
                        if (alloc.Inventory != null && !alloc.IsLooseShipment)
                        {
                            alloc.Inventory.Status = 11;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                TempData["SuccessMessage"] = $"{targetOutbounds.Count}件の「確認中」データを一括で確認済（予定）に更新しました。";
            }
            catch (Exception ex)
            {
                string rawDetail = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                string friendlyMessage = rawDetail.Contains("SqlServerRetryingExecutionStrategy")
                    ? "データベースのリトライ戦略による制限が発生しました。再度実行してください。"
                    : rawDetail;
                TempData["ErrorMessage"] = $"一括確定処理中にエラーが発生しました: {friendlyMessage}";
            }

            return RedirectToAction(nameof(ConfirmWarehouse), new { groupCode });
        }

        /// <summary>
        /// 画面上で見えているページ以外のすべての確認中データを確定します。
        /// </summary>
        /// <param name="groupCode">グループコード</param>
        /// <returns>確認画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ConfirmAllExceptVisible(string? groupCode)
        {
            return ConfirmAll(groupCode);
        }

        /// <summary>
        /// 指定された出庫データの編集フォーム画面を表示します。
        /// </summary>
        /// <param name="id">出庫ID</param>
        /// <returns>編集画面ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var record = await _context.Outbounds
                .Include(o => o.Shipper)
                .Include(o => o.Warehouse)
                .Include(o => o.Product)
                .Include(o => o.Carrier)
                .Include(o => o.ShippingClass)
                .FirstOrDefaultAsync(o => o.OutboundId == id);

            if (record == null)
            {
                TempData["ErrorMessage"] = "対象の出庫データが見つかりません。";
                return RedirectToAction(nameof(ConfirmWarehouse));
            }

            ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
            ViewBag.Carriers = await _context.Carriers.OrderBy(c => c.CarrierName).ToListAsync();
            ViewBag.ShippingClasses = await _context.ShippingClasses.OrderBy(s => s.ClassName).ToListAsync();

            return View(record);
        }

        /// <summary>
        /// 編集された出庫データを保存更新します。
        /// </summary>
        /// <param name="id">出庫ID</param>
        /// <param name="model">入力出庫モデル</param>
        /// <returns>確認画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Outbound model)
        {
            var record = await _context.Outbounds.FindAsync(id);
            if (record == null)
            {
                TempData["ErrorMessage"] = "対象の出庫データが見つかりません。";
                return RedirectToAction(nameof(ConfirmWarehouse));
            }

            record.WarehouseId = model.WarehouseId;
            record.CarrierId = model.CarrierId;
            record.ShippingType = model.ShippingType;
            record.SenderCode = model.SenderCode;
            record.ScheduledOutboundDate = model.ScheduledOutboundDate;
            record.CaseCount = model.CaseCount;
            record.Price = model.Price;
            record.DeliveryTimeClass = model.DeliveryTimeClass;
            record.RecipientCode = model.RecipientCode;
            record.ZipCode = model.ZipCode;
            record.Address1 = model.Address1;
            record.Address2 = model.Address2;
            record.Address3 = model.Address3;
            record.CompanyName1 = model.CompanyName1;
            record.CompanyName2 = model.CompanyName2;
            record.RecipientName = model.RecipientName;
            record.Tel = model.Tel;
            record.OutboundWeight = model.OutboundWeight;
            record.Notes = model.Notes;
            record.Status = model.Status;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "出庫データを更新しました。";
            return RedirectToAction(nameof(ConfirmWarehouse));
        }

        /// <summary>
        /// 出荷指示（Excel/CSV）の取り込み設定画面を表示します。
        /// </summary>
        /// <returns>取り込み画面ビュー</returns>
        /// <summary>
        /// 出荷指示（Excel/CSV）の取り込み設定画面を表示します。
        /// </summary>
        /// <returns>取り込み画面ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> ImportInstruction()
        {
            ViewBag.Shippers = await _context.Shippers.Where(s => !s.IsDeleted).OrderBy(s => s.ShipperName).ToListAsync();
            ViewBag.Projects = await _context.Projects.Where(p => !p.IsDeleted).OrderBy(p => p.ProjectName)
                .Select(p => new { p.ProjectId, p.ShipperId, p.ProjectName })
                .ToListAsync();
            return View();
        }

        /// <summary>
        /// 出荷指示ファイルを取り込み、最安倉庫選定アルゴリズム・FIFO在庫引き当てを自動実行します。
        /// </summary>
        /// <param name="shipperId">荷主ID</param>
        /// <param name="projectId">案件ID</param>
        /// <param name="weightSpec">重量計算仕様（30kg固定 or 商品マスタ）</param>
        /// <param name="skipHeader">ヘッダー行スキップフラグ</param>
        /// <param name="excelFile">アップロードファイル</param>
        /// <returns>出荷指示一覧へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportInstruction(
            Guid shipperId,
            Guid projectId,
            string weightSpec,
            bool skipHeader,
            IFormFile excelFile)
        {
            if (shipperId == Guid.Empty || projectId == Guid.Empty || excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "必須項目（荷主、案件、出荷指示ファイル）を入力してください。";
                return RedirectToAction(nameof(ImportInstruction));
            }

            string groupCode = "GRP-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper();
            bool is30KgFixed = weightSpec == "fixed30";
            int totalImported = 0;

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    using var stream = excelFile.OpenReadStream();
                    var rows = stream.Query(useHeaderRow: false).ToList();

                    int startRowIndex = skipHeader ? 1 : 0;
                    int importedCount = 0;

                    var shippingInstruction = new ShippingInstruction
                    {
                        ShippingInstructionId = Guid.NewGuid(),
                        ShippingInstructionGroup = groupCode,
                        FileName = Path.GetFileName(excelFile.FileName),
                        FileSize = excelFile.Length,
                        ShipperId = shipperId,
                        ProjectId = projectId,
                        WeightSpec = weightSpec,
                        ImportedCount = 0,
                        Status = 1
                    };
                    _context.ShippingInstructions.Add(shippingInstruction);
                    await _context.SaveChangesAsync();

                    var validProducts = await _context.Products.ToListAsync();
                    var validProductIds = new HashSet<string>(validProducts.Select(p => p.ProductId));

                    string GetValue(IDictionary<string, object> r, int index)
                    {
                        string key = ((char)('A' + index)).ToString();
                        if (r.TryGetValue(key, out var val))
                        {
                            return val?.ToString() ?? "";
                        }
                        return "";
                    }

                    var excelProductCodes = new HashSet<string>();
                    for (int i = startRowIndex; i < rows.Count; i++)
                    {
                        var rowDict = rows[i] as IDictionary<string, object>;
                        if (rowDict == null) continue;

                        string pCode = GetValue(rowDict, 9).Trim();
                        if (!string.IsNullOrWhiteSpace(pCode))
                        {
                            excelProductCodes.Add(pCode);
                        }
                    }

                    Guid? lastAdoptedCarrierId = null;

                    for (int i = startRowIndex; i < rows.Count; i++)
                    {
                        var rowDict = rows[i] as IDictionary<string, object>;
                        if (rowDict == null) continue;

                        string recipientCode = GetValue(rowDict, 0);
                        string productCode = GetValue(rowDict, 9);

                        if (string.IsNullOrWhiteSpace(recipientCode) && string.IsNullOrWhiteSpace(productCode)) continue;

                        string zipCode = GetValue(rowDict, 1).Replace("-", "").Trim();
                        if (zipCode.Length > 7)
                        {
                            zipCode = zipCode.Substring(0, 7);
                        }

                        string addr1 = GetValue(rowDict, 2);
                        string addr2 = GetValue(rowDict, 3);
                        string addr3 = GetValue(rowDict, 4);
                        string company1 = GetValue(rowDict, 5);
                        string company2 = GetValue(rowDict, 6);
                        string name = GetValue(rowDict, 7);
                        string tel = GetValue(rowDict, 8);
                        string productName = GetValue(rowDict, 10);
                        int? packQtyVal = int.TryParse(GetValue(rowDict, 11), out var pQty) && pQty > 0 ? pQty : null;
                        int.TryParse(GetValue(rowDict, 12), out var itemUnits);
                        string notes = GetValue(rowDict, 13);
                        string scheduledDateStr = GetValue(rowDict, 14);
                        string scheduledDeliveryDateStr = GetValue(rowDict, 15);
                        string deliveryTimeStr = GetValue(rowDict, 16);

                        string deliveryNoteApp1 = GetValue(rowDict, 17);
                        string deliveryNoteApp2 = GetValue(rowDict, 18);
                        string deliveryNoteNotes = GetValue(rowDict, 19);
                        string transportCode = GetValue(rowDict, 20);
                        string memo = GetValue(rowDict, 21);

                        DateTime? scheduledDate = null;
                        string[] dateFormats = { "yyyyMMdd", "yyyy/MM/dd", "yyyy-MM-dd", "yyyy/M/d", "yyyy-M-d" };
                        if (DateTime.TryParse(scheduledDateStr, out var dt))
                        {
                            scheduledDate = dt;
                        }
                        else if (DateTime.TryParseExact(scheduledDateStr?.Trim(), dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dtExact))
                        {
                            scheduledDate = dtExact;
                        }

                        DateTime? scheduledDeliveryDate = null;
                        if (DateTime.TryParse(scheduledDeliveryDateStr, out var dtDeliv))
                        {
                            scheduledDeliveryDate = dtDeliv;
                        }
                        else if (DateTime.TryParseExact(scheduledDeliveryDateStr?.Trim(), dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dtDelivExact))
                        {
                            scheduledDeliveryDate = dtDelivExact;
                        }

                        if (string.IsNullOrWhiteSpace(productCode))
                        {
                            throw new Exception($"{i + 1}行目: 商品コードが空です。");
                        }
                        if (!validProductIds.Contains(productCode))
                        {
                            throw new Exception($"{i + 1}行目: 商品コード '{productCode}' は商品マスタに存在しません。");
                        }

                        if (itemUnits <= 0) itemUnits = 1;

                        var cheapestOpt = await _cheapestWarehouseService.FindCheapestWarehouseOptionAsync(
                            shipperId, productCode, Guid.Empty, zipCode, itemUnits, is30KgFixed, projectId);

                        Guid? warehouseId = cheapestOpt.WarehouseId;
                        int rateTableType = cheapestOpt.RateTableType;
                        bool hasStock = cheapestOpt.HasStock;
                        bool isPriceNotFound = !cheapestOpt.IsPriceFound;
                        decimal? adoptedPrice = cheapestOpt.CalculatedPrice;

                        Guid carrierId = cheapestOpt.CarrierId ?? Guid.Empty;
                        if (carrierId == Guid.Empty)
                        {
                            var firstPwft = await _context.ProjectWarehouseFreightTables
                                .Include(pwf => pwf.FreightTable)
                                .FirstOrDefaultAsync(pwf => pwf.ProjectId == projectId && !pwf.IsDeleted);
                            carrierId = firstPwft?.FreightTable?.CarrierId ?? Guid.Empty;
                        }
                        if (carrierId != Guid.Empty)
                        {
                            lastAdoptedCarrierId = carrierId;
                        }

                        string? senderCode = null;

                        var shippingClass = await _context.ShippingClasses
                            .FirstOrDefaultAsync(s => s.CarrierId == carrierId && s.RateTableType == rateTableType && !s.IsDeleted)
                            ?? await _context.ShippingClasses.FirstOrDefaultAsync(s => s.CarrierId == carrierId && !s.IsDeleted);

                        if (shippingClass != null && warehouseId.HasValue)
                        {
                            var area = await _context.CollectionAreas
                                .FirstOrDefaultAsync(a => a.ShipperId == shipperId 
                                                       && a.ShippingClassId == shippingClass.ShippingClassId 
                                                       && a.WarehouseId == warehouseId.Value 
                                                       && !a.IsDeleted);
                            senderCode = area?.SenderCode;
                        }

                        var targetProduct = validProducts.FirstOrDefault(p => p.ProductId == productCode);
                        int unitQty = targetProduct?.Quantity > 0 ? targetProduct.Quantity : 1;
                        int initialCaseCount = itemUnits / unitQty + (itemUnits % unitQty > 0 ? 1 : 0);
                        if (initialCaseCount <= 0) initialCaseCount = 1;

                        int unitW = is30KgFixed ? 30 : (targetProduct?.Weight ?? 0);
                        int initialWeight = unitW * initialCaseCount;

                        int? deliveryTimeClass = int.TryParse(deliveryTimeStr, out var dTime) ? dTime : null;

                        var outbound = new Outbound
                        {
                            OutboundId = Guid.NewGuid(),
                            ShippingInstructionId = shippingInstruction.ShippingInstructionId,
                            ShipperId = shipperId,
                            WarehouseId = warehouseId,
                            ProductId = productCode,
                            CarrierId = carrierId,
                            ShippingInstructionGroup = groupCode,
                            ScheduledOutboundDate = scheduledDate,
                            ShippingType = shippingClass?.ShippingClassId ?? Guid.Empty,
                            PalletCount = 0,
                            TotalPieces = itemUnits,
                            CaseCount = initialCaseCount,
                            PackQty = packQtyVal,
                            OutboundWeight = initialWeight,
                            Price = adoptedPrice,
                            SenderCode = senderCode,
                            DeliveryTimeClass = deliveryTimeClass,
                            Status = !hasStock ? 998 : (isPriceNotFound ? 801 : 1),
                            RecipientCode = recipientCode,
                            ZipCode = zipCode,
                            Address1 = addr1,
                            Address2 = addr2,
                            Address3 = addr3,
                            CompanyName1 = company1,
                            CompanyName2 = company2,
                            RecipientName = name,
                            Tel = tel,
                            Notes = notes,
                            ScheduledDeliveryDate = scheduledDeliveryDate,
                            DeliveryNoteApp1 = deliveryNoteApp1,
                            DeliveryNoteApp2 = deliveryNoteApp2,
                            DeliveryNoteNotes = deliveryNoteNotes,
                            TransportCode = transportCode,
                            Memo = memo
                        };
                        _context.Outbounds.Add(outbound);
                        await _context.SaveChangesAsync();

                        if (hasStock && warehouseId.HasValue)
                        {
                            int calculatedCaseCount = await _cheapestWarehouseService.AllocateInventoryAsync(
                                outbound.OutboundId, warehouseId.Value, shipperId, productCode, itemUnits, outbound.ScheduledOutboundDate);
                            
                            outbound.CaseCount = calculatedCaseCount;

                            int unitWeight = is30KgFixed ? 30 : (targetProduct?.Weight ?? 0);
                            outbound.OutboundWeight = unitWeight * calculatedCaseCount;

                            _context.Outbounds.Update(outbound);
                            await _context.SaveChangesAsync();
                        }

                        importedCount++;
                    }

                    shippingInstruction.CarrierId = lastAdoptedCarrierId;
                    shippingInstruction.ImportedCount = importedCount;
                    _context.ShippingInstructions.Update(shippingInstruction);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    totalImported = importedCount;
                });

                TempData["SuccessMessage"] = $"出荷指示ファイルからの読込が完了しました。（出荷指示グループ: {groupCode}, 件数: {totalImported}件）";
                return RedirectToAction(nameof(ShippingInstructionList));
            }
            catch (Exception ex)
            {
                string rawDetail = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                string friendlyMessage = rawDetail.Contains("SqlServerRetryingExecutionStrategy")
                    ? "データベースのリトライ戦略による制限が発生しました。再度実行してください。"
                    : rawDetail;
                TempData["ErrorMessage"] = $"出荷指示読込エラー: {friendlyMessage}";
                return RedirectToAction(nameof(ImportInstruction));
            }
        }

        /// <summary>
        /// 取り込み済みの出荷指示グループ一覧を表示します。
        /// </summary>
        /// <param name="searchGroup">グループ名検索キーワード</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの件数</param>
        /// <returns>出荷指示一覧ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> ShippingInstructionList(string? searchGroup, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Outbound_InstructionList";
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

            var query = _context.ShippingInstructions
                .Include(s => s.Shipper)
                .Include(s => s.Project)
                .Include(s => s.Carrier)
                .Include(s => s.Outbounds)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchGroup))
            {
                query = query.Where(s => s.ShippingInstructionGroup.Contains(searchGroup) || (s.FileName != null && s.FileName.Contains(searchGroup)));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            var viewModelList = items.Select(s => {
                var activeOutbounds = s.Outbounds.Where(o => !o.IsDeleted).ToList();
                int pendingCount = activeOutbounds.Count(o => o.Status == 1);
                int priceNotFoundCount = activeOutbounds.Count(o => o.Status == 801);
                int outOfStockCount = activeOutbounds.Count(o => o.Status == 998);
                int confirmedCount = activeOutbounds.Count(o => o.Status != 1 && o.Status != 801 && o.Status != 998 && o.Status != 999);
                
                bool canCancel = confirmedCount == 0 && s.Status != 999;

                return new ShippingInstructionItemViewModel
                {
                    ShippingInstructionId = s.ShippingInstructionId,
                    ShippingInstructionGroup = s.ShippingInstructionGroup,
                    FileName = s.FileName,
                    ShipperName = s.Shipper?.ShipperName ?? "-",
                    ProjectName = s.Project?.ProjectName ?? "-",
                    CarrierName = s.Carrier?.CarrierName ?? "-",
                    ImportedCount = s.ImportedCount,
                    CreatedAt = s.CreatedAt,
                    CreatedBy = s.CreatedBy,
                    Status = s.Status,
                    PendingCount = pendingCount,
                    PriceNotFoundCount = priceNotFoundCount,
                    OutOfStockCount = outOfStockCount,
                    ConfirmedCount = confirmedCount,
                    CanCancel = canCancel
                };
            }).ToList();

            ViewBag.SearchGroup = searchGroup;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(viewModelList);
        }

        /// <summary>
        /// 指定された出荷指示グループの詳細（紐づく出庫データ一覧）を表示します。
        /// </summary>
        /// <param name="id">出荷指示ID</param>
        /// <param name="search">検索キーワード</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの件数</param>
        /// <returns>出荷指示詳細ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> ShippingInstructionDetail(Guid id, string? search, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Outbound_InstructionDetail";
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

            var shippingInstruction = await _context.ShippingInstructions
                .Include(s => s.Shipper)
                .Include(s => s.Carrier)
                .FirstOrDefaultAsync(s => s.ShippingInstructionId == id);

            if (shippingInstruction == null)
            {
                TempData["ErrorMessage"] = "対象の出荷指示が見つかりません。";
                return RedirectToAction(nameof(ShippingInstructionList));
            }

            var query = _context.Outbounds
                .Include(o => o.Shipper)
                .Include(o => o.Warehouse)
                .Include(o => o.Product)
                .Include(o => o.Carrier)
                .Include(o => o.ShippingClass)
                .Where(o => o.ShippingInstructionId == id || o.ShippingInstructionGroup == shippingInstruction.ShippingInstructionGroup)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(o => (o.RecipientCode != null && o.RecipientCode.Contains(search))
                                      || (o.RecipientName != null && o.RecipientName.Contains(search))
                                      || (o.CompanyName1 != null && o.CompanyName1.Contains(search))
                                      || o.ProductId.Contains(search)
                                      || (o.Product != null && o.Product.ProductName.Contains(search)));
            }

            int totalCount = await query.CountAsync();
            var outbounds = await query.OrderBy(o => o.RecipientCode).ThenBy(o => o.ProductId)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.ShippingInstruction = shippingInstruction;
            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);
            ViewBag.TotalCount = totalCount;

            return View(outbounds);
        }

        /// <summary>
        /// 指定された出荷指示グループを取り消し（紐づく出庫データを削除し、引き当て済み在庫を元に戻す）ます。
        /// </summary>
        /// <param name="id">出荷指示ID</param>
        /// <returns>出荷指示一覧へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelShippingInstruction(Guid id)
        {
            var instruction = await _context.ShippingInstructions
                .Include(s => s.Outbounds)
                .FirstOrDefaultAsync(s => s.ShippingInstructionId == id);

            if (instruction == null)
            {
                TempData["ErrorMessage"] = "対象の出荷指示が見つかりません。";
                return RedirectToAction(nameof(ShippingInstructionList));
            }

            if (instruction.Status == 999)
            {
                TempData["ErrorMessage"] = "この出荷指示はすでに取り消されています。";
                return RedirectToAction(nameof(ShippingInstructionList));
            }

            var activeOutbounds = instruction.Outbounds.Where(o => !o.IsDeleted).ToList();

            bool hasConfirmedData = activeOutbounds.Any(o => o.Status != 1 && o.Status != 801 && o.Status != 998);
            if (hasConfirmedData)
            {
                TempData["ErrorMessage"] = "出荷指示データ内に確認済（予定など）の出庫データが含まれているため、出荷指示の取り消しはできません。";
                return RedirectToAction(nameof(ShippingInstructionList));
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    foreach (var outbound in activeOutbounds)
                    {
                        await _cheapestWarehouseService.ReleaseInventoryAllocationAsync(outbound.OutboundId);

                        outbound.Status = 999;
                        outbound.IsDeleted = true;
                    }

                    instruction.Status = 999;
                    instruction.IsDeleted = true;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                TempData["SuccessMessage"] = $"出荷指示（出荷指示グループ: {instruction.ShippingInstructionGroup}）の取り消しが完了しました。紐づく出庫データが削除され、在庫が元に戻りました。";
            }
            catch (Exception ex)
            {
                string rawDetail = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                string friendlyMessage = rawDetail.Contains("SqlServerRetryingExecutionStrategy")
                    ? "データベースのリトライ戦略による制限が発生しました。再度実行してください。"
                    : rawDetail;
                TempData["ErrorMessage"] = $"出荷指示取り消し処理中にエラーが発生しました: {friendlyMessage}";
            }

            return RedirectToAction(nameof(ShippingInstructionList));
        }

        /// <summary>
        /// 送り状CSV出力対象（ステータスが「予定」のグループ）一覧を表示します。
        /// </summary>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの件数</param>
        /// <returns>送り状出力ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> ShippingLabel(int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Outbound_ShippingLabel";
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

            var activeKeysQuery = _context.Outbounds
                .Where(o => o.Status == 11)
                .Select(o => new { o.ShippingInstructionGroup, o.CarrierId, o.WarehouseId })
                .Distinct();

            int totalCount = await activeKeysQuery.CountAsync();
            var activeKeys = await activeKeysQuery
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            var list = new List<ShippingLabelGroupViewModel>();
            foreach (var key in activeKeys)
            {
                var rep = await _context.Outbounds
                    .Include(o => o.Shipper)
                    .Include(o => o.Warehouse)
                    .Include(o => o.Carrier)
                    .FirstOrDefaultAsync(o => o.ShippingInstructionGroup == key.ShippingInstructionGroup 
                                           && o.CarrierId == key.CarrierId 
                                           && o.WarehouseId == key.WarehouseId
                                           && o.Status == 11);
                if (rep != null)
                {
                    list.Add(new ShippingLabelGroupViewModel
                    {
                        ShippingInstructionGroup = key.ShippingInstructionGroup,
                        CarrierId = key.CarrierId,
                        CarrierName = rep.Carrier?.CarrierName ?? "-",
                        WarehouseId = key.WarehouseId,
                        WarehouseName = rep.Warehouse?.WarehouseName ?? "-",
                        ShipperName = rep.Shipper?.ShipperName ?? "-"
                    });
                }
            }

            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(list);
        }

        /// <summary>
        /// 指定グループ・運送会社・倉庫の送り状CSVを生成ダウンロードし、ステータスを「送状出力（出庫済）」に更新します。
        /// </summary>
        /// <param name="groupCode">グループコード</param>
        /// <param name="carrierId">運送会社ID</param>
        /// <param name="warehouseId">倉庫ID</param>
        /// <returns>CSVファイルレスポンス</returns>
        [HttpPost]
        public async Task<IActionResult> DownloadShippingLabel(string groupCode, Guid carrierId, Guid? warehouseId)
        {
            var records = await _context.Outbounds
                .Include(o => o.Shipper)
                .Include(o => o.Warehouse)
                .Include(o => o.Product)
                .Where(o => o.ShippingInstructionGroup == groupCode 
                         && o.CarrierId == carrierId 
                         && o.WarehouseId == warehouseId 
                         && o.Status == 11)
                .OrderBy(o => o.ProductId)
                .ToListAsync();

            if (!records.Any())
            {
                TempData["ErrorMessage"] = "出力対象の「予定」データが見つかりません。";
                return RedirectToAction(nameof(ShippingLabel));
            }

            int currentNum = 1;
            string counterFile = Path.Combine("C:\\Users\\merit\\.gemini\\antigravity-ide", "invoice_counter.txt");
            try
            {
                if (System.IO.File.Exists(counterFile))
                {
                    if (int.TryParse(System.IO.File.ReadAllText(counterFile), out var val))
                    {
                        currentNum = val;
                    }
                }
            }
            catch { }

            string[] headers = new string[]
            {
                "出荷予定日","管理番号","お問合せ番号","元着区分","原票区分",
                "個数","重量区分","重量（Ｋ)","重量（才）","荷送人コード",
                "荷送人名称","荷送人住所１","荷送人住所２","荷送人電話番号","部署コード",
                "部署名","お届け先コード","お届け先郵便番号","お届け先名称１","お届け先名称２",
                "お届け先住所１","お届け先住所２","お届け先電話番号","お届け先JIS市町村コード","止商品区分",
                "止指定店名称","保険金額","輸送指示コード１","輸送指示1","輸送指示コード２",
                "輸送指示2","配達指定区分","記事コード１","記事１","記事コード２",
                "記事２","記事コード３","記事３","記事コード４","記事４",
                "記事コード５","記事５","出荷一覧表印刷日","出荷情報登録日","出荷情報更新日"
            };

            var csvBytes = CsvService.ExportToCsvBytes(records, headers, r =>
            {
                int controlNo = currentNum++;
                if (currentNum > 999999) currentNum = 1;

                return new string[]
                {
                    r.ScheduledOutboundDate?.ToString("yyyy/MM/dd") ?? "",
                    controlNo.ToString(),
                    "",
                    "1",
                    "0",
                    r.CaseCount.ToString(),
                    "",
                    r.OutboundWeight?.ToString() ?? "",
                    "",
                    r.SenderCode ?? "",
                    r.Shipper?.ShipperName ?? "",
                    r.Shipper?.ShipperAddress1 ?? "",
                    r.Shipper?.ShipperAddress2 ?? "",
                    r.Shipper?.ShipperTel ?? "",
                    "",
                    "",
                    "",
                    r.ZipCode ?? "",
                    r.CompanyName1 ?? "",
                    r.CompanyName2 ?? "",
                    r.Address1 ?? "",
                    r.Address2 ?? "",
                    r.Tel ?? "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    r.DeliveryTimeClass?.ToString() ?? "",
                    "",
                    "",
                    "",
                    r.Product?.ProductName ?? "",
                    "",
                    r.Product?.ProductName ?? "",
                    "",
                    r.Product?.ProductName ?? "",
                    "",
                    "",
                    "",
                    "",
                    ""
                };
            });

            try
            {
                System.IO.File.WriteAllText(counterFile, currentNum.ToString());
            }
            catch { }

            foreach (var r in records)
            {
                r.Status = 21;
            }

            var outboundIds = records.Select(r => r.OutboundId).ToList();
            var allocations = await _context.OutboundAllocations
                .Include(a => a.Inventory)
                .Where(a => outboundIds.Contains(a.OutboundId) && !a.IsDeleted)
                .ToListAsync();

            foreach (var alloc in allocations)
            {
                alloc.Status = 21;
                if (alloc.Inventory != null)
                {
                    if (!alloc.IsLooseShipment || alloc.Inventory.CurrentQuantity <= 0)
                    {
                        alloc.Inventory.Status = 21;
                        alloc.Inventory.ActualOutboundDate = DateTime.Now;
                    }
                }
            }

            await _context.SaveChangesAsync();

            var filename = $"送り状_{groupCode}_{DateTime.Now:yyyyMMddHHmmss}.csv";
            return File(csvBytes, "text/csv", filename);
        }

        /// <summary>
        /// 送り状CSV出力前のデータプレビュー情報をJSON形式で取得します。
        /// </summary>
        /// <param name="groupCode">グループコード</param>
        /// <param name="carrierId">運送会社ID</param>
        /// <param name="warehouseId">倉庫ID</param>
        /// <returns>プレビュー情報JSON</returns>
        [HttpGet]
        public async Task<IActionResult> GetShippingLabelPreview(string groupCode, Guid carrierId, Guid? warehouseId)
        {
            var records = await _context.Outbounds
                .Include(o => o.Shipper)
                .Include(o => o.Warehouse)
                .Include(o => o.Carrier)
                .Include(o => o.Product)
                .Where(o => o.ShippingInstructionGroup == groupCode 
                         && o.CarrierId == carrierId 
                         && o.WarehouseId == warehouseId 
                         && o.Status == 11)
                .OrderBy(o => o.ProductId)
                .ToListAsync();

            if (!records.Any())
            {
                return NotFound("対象となる「予定」データが見つかりません。");
            }

            int currentNum = 1;
            string counterFile = Path.Combine("C:\\Users\\merit\\.gemini\\antigravity-ide", "invoice_counter.txt");
            try
            {
                if (System.IO.File.Exists(counterFile))
                {
                    if (int.TryParse(System.IO.File.ReadAllText(counterFile), out var val))
                    {
                        currentNum = val;
                    }
                }
            }
            catch { }

            string[] headers = new string[]
            {
                "出荷予定日","管理番号","お問合せ番号","元着区分","原票区分",
                "個数","重量区分","重量（Ｋ)","重量（才）","荷送人コード",
                "荷送人名称","荷送人住所１","荷送人住所２","荷送人電話番号","部署コード",
                "部署名","お届け先コード","お届け先郵便番号","お届け先名称１","お届け先名称２",
                "お届け先住所１","お届け先住所２","お届け先電話番号","お届け先JIS市町村コード","止商品区分",
                "止指定店名称","保険金額","輸送指示コード１","輸送指示1","輸送指示コード２",
                "輸送指示2","配達指定区分","記事コード１","記事１","記事コード２",
                "記事２","記事コード３","記事３","記事コード４","記事４",
                "記事コード５","記事５","出荷一覧表印刷日","出荷情報登録日","出荷情報更新日"
            };

            int simNo = currentNum;
            var rows = records.Select(r =>
            {
                int controlNo = simNo++;
                if (simNo > 999999) simNo = 1;

                return new string[]
                {
                    r.ScheduledOutboundDate?.ToString("yyyy/MM/dd") ?? "",
                    controlNo.ToString(),
                    "",
                    "1",
                    "0",
                    r.CaseCount.ToString(),
                    "",
                    r.OutboundWeight?.ToString() ?? "",
                    "",
                    r.SenderCode ?? "",
                    r.Shipper?.ShipperName ?? "",
                    r.Shipper?.ShipperAddress1 ?? "",
                    r.Shipper?.ShipperAddress2 ?? "",
                    r.Shipper?.ShipperTel ?? "",
                    "",
                    "",
                    "",
                    r.ZipCode ?? "",
                    r.CompanyName1 ?? "",
                    r.CompanyName2 ?? "",
                    r.Address1 ?? "",
                    r.Address2 ?? "",
                    r.Tel ?? "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    r.DeliveryTimeClass?.ToString() ?? "",
                    "",
                    "",
                    "",
                    r.Product?.ProductName ?? "",
                    "",
                    r.Product?.ProductName ?? "",
                    "",
                    r.Product?.ProductName ?? "",
                    "",
                    "",
                    "",
                    "",
                    ""
                };
            }).ToList();

            var first = records.First();

            return Json(new
            {
                groupCode = groupCode,
                shipperName = first.Shipper?.ShipperName ?? "-",
                carrierName = first.Carrier?.CarrierName ?? "-",
                warehouseName = first.Warehouse?.WarehouseName ?? "-",
                recordCount = records.Count,
                headers = headers,
                rows = rows
            });
        }

        /// <summary>
        /// 指定された出庫データについて、各倉庫・運賃表における配送金額・在庫状況を比較する画面を表示します。
        /// </summary>
        /// <param name="id">出庫ID</param>
        /// <returns>運賃比較ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> CompareRates(Guid id)
        {
            var outbound = await _context.Outbounds
                .Include(o => o.Shipper)
                .Include(o => o.Product)
                .Include(o => o.Carrier)
                .Include(o => o.ShippingClass)
                .FirstOrDefaultAsync(o => o.OutboundId == id);

            if (outbound == null)
            {
                return NotFound();
            }

            int size = 30;
            if (outbound.OutboundWeight.HasValue)
            {
                size = outbound.OutboundWeight.Value;
            }

            var cleanZip = outbound.ZipCode?.Replace("-", "").Trim() ?? "";
            var zipEntry = await _context.ZipCodes.FirstOrDefaultAsync(z => z.ZipCodeValue == cleanZip);
            var cityName = zipEntry != null ? $"コード: {zipEntry.CityCode} ({zipEntry.PrefCode})" : "-";
            var cityCode = zipEntry?.CityCode;
            var prefCode = zipEntry?.PrefCode;

            var currentShippingClass = await _context.ShippingClasses.FirstOrDefaultAsync(s => s.ShippingClassId == outbound.ShippingType);
            int currentRateTableType = currentShippingClass?.RateTableType ?? 0;

            var projectIds = await _context.Projects.Where(p => p.ShipperId == outbound.ShipperId && !p.IsDeleted).Select(p => p.ProjectId).ToListAsync();
            var rateMappings = await _context.ProjectWarehouseFreightTables
                .Include(w => w.FreightTable)
                .Where(w => projectIds.Contains(w.ProjectId) && w.FreightTable!.CarrierId == outbound.CarrierId && !w.IsDeleted)
                .ToListAsync();

            var mappedWarehouseIds = rateMappings.Select(w => w.WarehouseId).Distinct().ToList();

            if (outbound.WarehouseId.HasValue && !mappedWarehouseIds.Contains(outbound.WarehouseId.Value))
            {
                mappedWarehouseIds.Add(outbound.WarehouseId.Value);
            }

            int unitQuantity = outbound.Product?.Quantity > 0 ? outbound.Product.Quantity : 1;
            int requiredPieces = outbound.TotalPieces > 0 ? outbound.TotalPieces : (outbound.CaseCount * unitQuantity);

            var selfAllocations = await _context.OutboundAllocations
                .Include(a => a.Inventory)
                .Where(a => a.OutboundId == outbound.OutboundId && !a.IsDeleted && a.Inventory != null)
                .ToListAsync();

            var selfAllocDict = selfAllocations.GroupBy(a => a.Inventory!.WarehouseId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        AllocatedPieces = g.Sum(x => x.AllocatedQuantity),
                        AllocatedCases = g.Select(x => x.InventoryId).Distinct().Count()
                    }
                );

            var freeStockSums = await _context.Inventories
                .Include(inv => inv.Product)
                .Where(inv => inv.ShipperId == outbound.ShipperId && inv.ProductId == outbound.ProductId && inv.Status == 1 && mappedWarehouseIds.Contains(inv.WarehouseId) && !inv.IsDeleted)
                .ToListAsync();

            var freeStockDict = freeStockSums.GroupBy(inv => inv.WarehouseId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        AvailablePieces = g.Sum(x => x.CurrentQuantity > 0 ? x.CurrentQuantity : (x.Product?.Quantity > 0 ? x.Product.Quantity : 1)),
                        AvailableCases = g.Count()
                    }
                );

            var stockDict = new Dictionary<Guid, (int AvailablePieces, int AvailableCases)>();
            foreach (var whId in mappedWarehouseIds)
            {
                freeStockDict.TryGetValue(whId, out var freeInfo);
                selfAllocDict.TryGetValue(whId, out var selfInfo);

                int totalPieces = (freeInfo?.AvailablePieces ?? 0) + (selfInfo?.AllocatedPieces ?? 0);
                int totalCases = (freeInfo?.AvailableCases ?? 0) + (selfInfo?.AllocatedCases ?? 0);

                stockDict[whId] = (totalPieces, totalCases);
            }

            var warehouses = await _context.Warehouses
                .Where(w => mappedWarehouseIds.Contains(w.WarehouseId) && !w.IsDeleted)
                .ToListAsync();

            var candidates = new List<WarehouseRateCandidate>();

            foreach (var wh in warehouses)
            {
                bool hasStockInfo = stockDict.TryGetValue(wh.WarehouseId, out var stockInfo);
                int availPieces = hasStockInfo ? stockInfo.AvailablePieces : 0;
                int availCases = hasStockInfo ? stockInfo.AvailableCases : 0;

                var whMappings = rateMappings.Where(w => w.WarehouseId == wh.WarehouseId).ToList();

                if (!whMappings.Any())
                {
                    bool isCurrentlyAdopted = (outbound.WarehouseId == wh.WarehouseId);
                    candidates.Add(new WarehouseRateCandidate
                    {
                        WarehouseId = wh.WarehouseId,
                        WarehouseName = wh.WarehouseName,
                        AvailableStock = availCases,
                        AvailableCases = availCases,
                        AvailablePieces = availPieces,
                        ZipCode = cleanZip,
                        CityCode = cityCode,
                        IsAdopted = isCurrentlyAdopted,
                        StatusMessage = isCurrentlyAdopted ? "現在採用中 (運賃表マッピング無し)" : "運賃表マッピング無し",
                        IsSelectable = false
                    });
                    continue;
                }

                foreach (var mapping in whMappings)
                {
                    var ft = mapping.FreightTable;
                    if (ft == null) continue;

                    bool isCurrentlyAdopted = (outbound.WarehouseId == wh.WarehouseId) && (ft.RateTableType == currentRateTableType);

                    var candidate = new WarehouseRateCandidate
                    {
                        WarehouseId = wh.WarehouseId,
                        WarehouseName = wh.WarehouseName + (whMappings.Count > 1 ? $" [{ft.RateName}]" : ""),
                        AvailableStock = availCases,
                        AvailableCases = availCases,
                        AvailablePieces = availPieces,
                        ZipCode = cleanZip,
                        CityCode = cityCode,
                        FreightTableId = ft.FreightTableId,
                        RateTableType = ft.RateTableType,
                        IsAdopted = isCurrentlyAdopted
                    };

                    if (ft.RateTableType == 1)
                    {
                        candidate.Size = outbound.CaseCount;

                        if (string.IsNullOrEmpty(prefCode))
                        {
                            candidate.StatusMessage = isCurrentlyAdopted ? "現在採用中 (都道府県不明)" : "都道府県コード不明";
                            candidate.IsSelectable = false;
                        }
                        else
                        {
                            int unitWeight = outbound.Product?.Weight > 0 ? outbound.Product.Weight : 30;
                            string targetPref = prefCode.Trim();
                            string targetPrefNoZero = targetPref.TrimStart('0');

                            var indFreightList = await _context.IndividualFreights
                                .Where(i => i.FreightTableId == ft.FreightTableId && !i.IsDeleted &&
                                           (i.PrefCode.Trim() == targetPref || i.PrefCode.Trim() == targetPrefNoZero))
                                .ToListAsync();

                            if (!indFreightList.Any())
                            {
                                var carrierFtIds = await _context.FreightTables
                                    .Where(f => f.CarrierId == ft.CarrierId && f.RateTableType == 1 && !f.IsDeleted)
                                    .Select(f => f.FreightTableId)
                                    .ToListAsync();

                                indFreightList = await _context.IndividualFreights
                                    .Where(i => carrierFtIds.Contains(i.FreightTableId) && !i.IsDeleted &&
                                               (i.PrefCode.Trim() == targetPref || i.PrefCode.Trim() == targetPrefNoZero))
                                    .ToListAsync();
                            }

                            if (!indFreightList.Any())
                            {
                                indFreightList = await _context.IndividualFreights
                                    .Where(i => !i.IsDeleted && (i.PrefCode.Trim() == targetPref || i.PrefCode.Trim() == targetPrefNoZero))
                                    .ToListAsync();
                            }

                            IndividualFreight? indFreight = null;
                            if (indFreightList.Any())
                            {
                                indFreight = indFreightList.Where(i => (i.Weight > 0 && i.Weight >= unitWeight) || (i.Size > 0 && i.Size >= unitWeight))
                                                           .OrderBy(i => i.Weight > 0 ? i.Weight : (i.Size > 0 ? i.Size : 999999))
                                                           .FirstOrDefault()
                                             ?? indFreightList.OrderBy(i => i.Price).FirstOrDefault();
                            }

                            if (indFreight != null)
                            {
                                int totalPrice = indFreight.Price * outbound.CaseCount;
                                candidate.PricePerUnit = indFreight.Price;
                                candidate.TotalPrice = totalPrice;

                                if (availPieces < requiredPieces)
                                {
                                    int reqCases = (int)Math.Ceiling((double)requiredPieces / unitQuantity);
                                    candidate.StatusMessage = isCurrentlyAdopted
                                        ? $"現在採用中 (在庫不足: 在庫 {availCases}箱[{availPieces}個])"
                                        : $"在庫不足 (必要: {reqCases}箱[{requiredPieces}個] / 在庫: {availCases}箱[{availPieces}個])";
                                    candidate.IsSelectable = false;
                                }
                                else
                                {
                                    if (isCurrentlyAdopted)
                                    {
                                        candidate.StatusMessage = "現在採用中 (個配)";
                                        candidate.IsSelectable = false;
                                    }
                                    else
                                    {
                                        candidate.StatusMessage = "選択可能 (個配)";
                                        candidate.IsSelectable = true;
                                    }
                                }
                            }
                            else
                            {
                                candidate.StatusMessage = isCurrentlyAdopted ? "現在採用中 (個配運賃定義無し)" : "該当都道府県の個配運賃無し";
                                candidate.IsSelectable = false;
                            }
                        }
                    }
                    else
                    {
                        int? distanceKm = null;
                        if (!string.IsNullOrEmpty(cityCode))
                        {
                            var distance = await _context.Distances
                                .Where(d => d.CityCode == cityCode && d.FreightTableId == ft.FreightTableId && !d.IsDeleted)
                                .FirstOrDefaultAsync();
                            if (distance != null)
                            {
                                distanceKm = distance.DistanceKm;
                            }
                        }
                        candidate.DistanceKm = distanceKm;

                        if (!distanceKm.HasValue)
                        {
                            candidate.StatusMessage = isCurrentlyAdopted ? "現在採用中 (距離設定無し)" : "距離設定無し";
                            candidate.IsSelectable = false;
                        }
                        else
                        {
                            var availableSizes = await _context.DistanceFreights
                                .Where(f => f.FreightTableId == ft.FreightTableId && !f.IsDeleted)
                                .Select(f => f.Size)
                                .Distinct()
                                .ToListAsync();

                            int targetSize = size;
                            if (availableSizes.Any() && !availableSizes.Contains(targetSize))
                            {
                                var ceilingSize = availableSizes.Where(s => s >= targetSize).OrderBy(s => s).Cast<int?>().FirstOrDefault();
                                targetSize = ceilingSize ?? availableSizes.Max();
                            }
                            candidate.Size = targetSize;

                            if (availableSizes.Any())
                            {
                                var freight = await _context.DistanceFreights
                                    .Where(f => f.FreightTableId == ft.FreightTableId
                                             && f.Size == targetSize
                                             && f.DistanceKm >= distanceKm.Value
                                             && !f.IsDeleted)
                                    .OrderBy(f => f.DistanceKm)
                                    .FirstOrDefaultAsync()
                                    ?? await _context.DistanceFreights
                                    .Where(f => f.FreightTableId == ft.FreightTableId
                                             && f.Size == targetSize
                                             && !f.IsDeleted)
                                    .OrderByDescending(f => f.DistanceKm)
                                    .FirstOrDefaultAsync();

                                if (freight != null)
                                {
                                    candidate.PricePerUnit = freight.Price;
                                    candidate.TotalPrice = freight.Price;
                                    candidate.FreightId = freight.FreightId;
                                    candidate.Size = freight.Size;

                                    if (availPieces < requiredPieces)
                                    {
                                        int reqCases = (int)Math.Ceiling((double)requiredPieces / unitQuantity);
                                        candidate.StatusMessage = isCurrentlyAdopted
                                            ? $"現在採用中 (在庫不足: 在庫 {availCases}箱[{availPieces}個])"
                                            : $"在庫不足 (必要: {reqCases}箱[{requiredPieces}個] / 在庫: {availCases}箱[{availPieces}個])";
                                        candidate.IsSelectable = false;
                                    }
                                    else
                                    {
                                        if (isCurrentlyAdopted)
                                        {
                                            candidate.StatusMessage = "現在採用中 (路線)";
                                            candidate.IsSelectable = false;
                                        }
                                        else
                                        {
                                            candidate.StatusMessage = "選択可能 (路線)";
                                            candidate.IsSelectable = true;
                                        }
                                    }
                                }
                                else
                                {
                                    candidate.StatusMessage = isCurrentlyAdopted ? "現在採用中 (該当路線運賃無し)" : "該当路線運賃の定義無し";
                                    candidate.IsSelectable = false;
                                }
                            }
                            else
                            {
                                candidate.StatusMessage = isCurrentlyAdopted ? "現在採用中 (路線運賃データ無し)" : "路線運賃データ無し";
                                candidate.IsSelectable = false;
                            }
                        }
                    }

                    candidates.Add(candidate);
                }
            }

            var viewModel = new CompareRatesViewModel
            {
                Outbound = outbound,
                CityName = cityName,
                Candidates = candidates.OrderByDescending(c => c.IsSelectable).ThenBy(c => c.TotalPrice).ToList()
            };

            return View(viewModel);
        }

        /// <summary>
        /// 運賃比較画面から選択された倉庫・運賃表へ手動で切り替え、在庫引当を再実行します。
        /// </summary>
        /// <param name="id">出庫ID</param>
        /// <param name="warehouseId">選択倉庫ID</param>
        /// <param name="freightTableId">選択運賃表ID</param>
        /// <returns>確認画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectWarehouse(Guid id, Guid warehouseId, Guid? freightTableId)
        {
            var outbound = await _context.Outbounds.FindAsync(id);
            if (outbound == null)
            {
                return NotFound();
            }

            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null)
            {
                return BadRequest("選択された倉庫が存在しません。");
            }

            var projectIds = await _context.Projects.Where(p => p.ShipperId == outbound.ShipperId && !p.IsDeleted).Select(p => p.ProjectId).ToListAsync();
            ProjectWarehouseFreightTable? rateMapping = null;
            if (freightTableId.HasValue && freightTableId != Guid.Empty)
            {
                rateMapping = await _context.ProjectWarehouseFreightTables
                    .Include(w => w.FreightTable)
                    .FirstOrDefaultAsync(w => projectIds.Contains(w.ProjectId) && w.WarehouseId == warehouseId && w.FreightTableId == freightTableId.Value && !w.IsDeleted);
            }

            if (rateMapping == null)
            {
                rateMapping = await _context.ProjectWarehouseFreightTables
                    .Include(w => w.FreightTable)
                    .FirstOrDefaultAsync(w => projectIds.Contains(w.ProjectId) && w.WarehouseId == warehouseId && w.FreightTable!.CarrierId == outbound.CarrierId && !w.IsDeleted);
            }

            if (rateMapping?.FreightTable == null)
            {
                return BadRequest("選択された倉庫と運送会社のマッピングが存在しません。");
            }

            var ft = rateMapping.FreightTable;
            var cleanZip = outbound.ZipCode?.Replace("-", "").Trim() ?? "";
            var zipEntry = await _context.ZipCodes.FirstOrDefaultAsync(z => z.ZipCodeValue == cleanZip);
            var cityCode = zipEntry?.CityCode;
            var prefCode = zipEntry?.PrefCode;

            decimal? adoptedPrice = null;
            int rateTableType = ft.RateTableType;

            if (ft.RateTableType == 1)
            {
                if (string.IsNullOrEmpty(prefCode))
                {
                    return BadRequest("お届け先の郵便番号に対応する都道府県コードがマスタに存在しません。");
                }
                var indFreight = await _context.IndividualFreights
                    .FirstOrDefaultAsync(i => i.FreightTableId == ft.FreightTableId && i.PrefCode == prefCode && !i.IsDeleted);

                if (indFreight == null)
                {
                    return BadRequest("選択された倉庫の個配運賃設定が存在しません。");
                }
                adoptedPrice = indFreight.Price * outbound.CaseCount;
            }
            else
            {
                if (string.IsNullOrEmpty(cityCode))
                {
                    return BadRequest("お届け先の郵便番号に対応する市区町村がマスタに存在しません。");
                }

                var distance = await _context.Distances
                    .Where(d => d.CityCode == cityCode 
                             && d.FreightTableId == ft.FreightTableId 
                             && !d.IsDeleted)
                    .FirstOrDefaultAsync();

                if (distance == null)
                {
                    return BadRequest("選択された倉庫と届け先住所の距離設定が存在しません。");
                }

                int size = outbound.OutboundWeight ?? 30;

                var availableSizes = await _context.DistanceFreights
                    .Where(f => f.FreightTableId == ft.FreightTableId && !f.IsDeleted)
                    .Select(f => f.Size)
                    .Distinct()
                    .ToListAsync();

                int targetSize = size;
                if (availableSizes.Any() && !availableSizes.Contains(targetSize))
                {
                    var ceilingSize = availableSizes.Where(s => s >= targetSize).OrderBy(s => s).Cast<int?>().FirstOrDefault();
                    targetSize = ceilingSize ?? availableSizes.Max();
                }

                var freight = await _context.DistanceFreights
                    .Where(f => f.FreightTableId == ft.FreightTableId 
                             && f.Size == targetSize 
                             && f.DistanceKm >= distance.DistanceKm 
                             && !f.IsDeleted)
                    .OrderBy(f => f.DistanceKm)
                    .FirstOrDefaultAsync()
                    ?? await _context.DistanceFreights
                    .Where(f => f.FreightTableId == ft.FreightTableId 
                             && f.Size == targetSize 
                             && !f.IsDeleted)
                    .OrderByDescending(f => f.DistanceKm)
                    .FirstOrDefaultAsync();

                if (freight == null)
                {
                    return BadRequest("選択された倉庫に対応する路線運賃設定が存在しません。");
                }
                adoptedPrice = freight.Price;
            }

            int availablePieces = await _context.Inventories
                .Where(inv => inv.ShipperId == outbound.ShipperId && inv.ProductId == outbound.ProductId && inv.Status == 1 && inv.WarehouseId == warehouseId && !inv.IsDeleted)
                .SumAsync(inv => inv.CurrentQuantity);

            if (availablePieces < outbound.TotalPieces)
            {
                return BadRequest($"在庫が不足しています（現在の有効在庫: {availablePieces}個）。他の倉庫を選択してください。");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    await _cheapestWarehouseService.ReleaseInventoryAllocationAsync(outbound.OutboundId);

                    int newCaseCount = await _cheapestWarehouseService.AllocateInventoryAsync(
                        outbound.OutboundId, warehouseId, outbound.ShipperId, outbound.ProductId, outbound.TotalPieces, outbound.ScheduledOutboundDate);

                    var shippingClass = await _context.ShippingClasses
                        .FirstOrDefaultAsync(s => s.CarrierId == outbound.CarrierId && s.RateTableType == rateTableType && !s.IsDeleted)
                        ?? await _context.ShippingClasses.FirstOrDefaultAsync(s => s.CarrierId == outbound.CarrierId && !s.IsDeleted);

                    if (shippingClass != null)
                    {
                        outbound.ShippingType = shippingClass.ShippingClassId;

                        var area = await _context.CollectionAreas
                            .FirstOrDefaultAsync(a => a.ShipperId == outbound.ShipperId 
                                                   && a.ShippingClassId == shippingClass.ShippingClassId 
                                                   && a.WarehouseId == warehouseId 
                                                   && !a.IsDeleted);
                        outbound.SenderCode = area?.SenderCode;
                    }

                    outbound.WarehouseId = warehouseId;
                    outbound.CaseCount = newCaseCount;
                    outbound.Price = adoptedPrice;

                    var validProduct = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == outbound.ProductId);
                    int unitWeight = (validProduct?.Weight ?? 0);
                    outbound.OutboundWeight = unitWeight > 0 ? (unitWeight * outbound.TotalPieces) : (30 * newCaseCount);

                    if (outbound.Status == 801)
                    {
                        outbound.Status = 1;
                    }

                    _context.Outbounds.Update(outbound);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                TempData["SuccessMessage"] = "出荷倉庫を決定し、在庫を引き当てました。";
            }
            catch (Exception ex)
            {
                string rawDetail = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                string friendlyMessage = rawDetail.Contains("SqlServerRetryingExecutionStrategy")
                    ? "データベースのリトライ戦略による制限が発生しました。再度実行してください。"
                    : rawDetail;
                return StatusCode(500, $"倉庫設定処理中にエラーが発生しました: {friendlyMessage}");
            }

            return RedirectToAction(nameof(ConfirmWarehouse));
        }

        /// <summary>
        /// 指定された運賃設定の計算根拠詳細モーダル・画面データを表示します。
        /// </summary>
        /// <param name="outboundId">出庫ID</param>
        /// <param name="warehouseId">倉庫ID</param>
        /// <param name="freightId">運賃ID</param>
        /// <returns>運賃詳細ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> FreightDetail(Guid outboundId, Guid warehouseId, Guid freightId)
        {
            var outbound = await _context.Outbounds
                .Include(o => o.Product)
                .Include(o => o.Carrier)
                .FirstOrDefaultAsync(o => o.OutboundId == outboundId);

            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            var freight = await _context.DistanceFreights
                .Include(f => f.FreightTable)
                .FirstOrDefaultAsync(f => f.FreightId == freightId);

            if (outbound == null || warehouse == null || freight == null)
            {
                return NotFound("対象のデータが見つかりません。");
            }

            var cleanZip = outbound.ZipCode?.Replace("-", "").Trim() ?? "";
            var zipEntry = await _context.ZipCodes.FirstOrDefaultAsync(z => z.ZipCodeValue == cleanZip);
            var cityCode = zipEntry?.CityCode ?? "";

            int? actualDistanceKm = null;
            if (!string.IsNullOrEmpty(cityCode))
            {
                var projectIds = await _context.Projects.Where(p => p.ShipperId == outbound.ShipperId && !p.IsDeleted).Select(p => p.ProjectId).ToListAsync();
                var rateMapping = await _context.ProjectWarehouseFreightTables
                    .FirstOrDefaultAsync(w => projectIds.Contains(w.ProjectId) && w.WarehouseId == warehouseId && w.FreightTable!.CarrierId == outbound.CarrierId && !w.IsDeleted);

                if (rateMapping != null)
                {
                    var distance = await _context.Distances
                        .FirstOrDefaultAsync(d => d.CityCode == cityCode && d.FreightTableId == rateMapping.FreightTableId && !d.IsDeleted);
                    actualDistanceKm = distance?.DistanceKm;
                }
            }

            var viewModel = new FreightDetailViewModel
            {
                Outbound = outbound,
                Warehouse = warehouse,
                DistanceFreight = freight,
                CityCode = cityCode,
                ActualDistanceKm = actualDistanceKm
            };

            return View(viewModel);
        }
    }

    /// <summary>
    /// 運賃計算根拠詳細ビューモデル
    /// </summary>
    public class FreightDetailViewModel
    {
        public Outbound Outbound { get; set; } = null!;
        public Warehouse Warehouse { get; set; } = null!;
        public DistanceFreight DistanceFreight { get; set; } = null!;
        public string CityCode { get; set; } = "";
        public int? ActualDistanceKm { get; set; }
    }

    /// <summary>
    /// 送り状CSV出力グループビューモデル
    /// </summary>
    public class ShippingLabelGroupViewModel
    {
        public string ShippingInstructionGroup { get; set; } = "";
        public Guid CarrierId { get; set; }
        public string CarrierName { get; set; } = "";
        public Guid? WarehouseId { get; set; }
        public string WarehouseName { get; set; } = "";
        public string ShipperName { get; set; } = "";
    }

    /// <summary>
    /// 運賃比較画面ビューモデル
    /// </summary>
    public class CompareRatesViewModel
    {
        public Outbound Outbound { get; set; } = null!;
        public string CityName { get; set; } = "-";
        public List<WarehouseRateCandidate> Candidates { get; set; } = new();
    }

    /// <summary>
    /// 各倉庫・運賃表候補ビューモデル
    /// </summary>
    public class WarehouseRateCandidate
    {
        public Guid WarehouseId { get; set; }
        public string WarehouseName { get; set; } = "";
        public int AvailableStock { get; set; }
        public int AvailableCases { get; set; }
        public int AvailablePieces { get; set; }
        public int? DistanceKm { get; set; }
        public int? PricePerUnit { get; set; }
        public int? TotalPrice { get; set; }
        public string StatusMessage { get; set; } = "";
        public bool IsSelectable { get; set; }
        public string? ZipCode { get; set; }
        public string? CityCode { get; set; }
        public int Size { get; set; }
        public Guid? FreightId { get; set; }
        public Guid? FreightTableId { get; set; }
        public int RateTableType { get; set; }
        public bool IsAdopted { get; set; }
    }
}
