using System;
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
    /// 入荷登録、確定、一覧照会、CSV取り込みを制御するコントローラー
    /// </summary>
    public class InboundController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public InboundController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 入荷データ一覧画面を表示します。
        /// </summary>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの件数</param>
        /// <returns>入荷一覧ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> List(int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Inbound_List";
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

            var query = _context.Inbounds
                .Include(i => i.Shipper)
                .Include(i => i.Warehouse)
                .Include(i => i.Product)
                .AsQueryable();

            int totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            ViewBag.Shippers = await _context.Shippers.OrderBy(s => s.ShipperName).ToListAsync();
            ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();

            return View(items);
        }

        /// <summary>
        /// 入荷データ新規登録・編集画面を表示します。
        /// </summary>
        /// <param name="id">編集対象の入荷ID（新規時はnull）</param>
        /// <returns>登録画面ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> Register(Guid? id)
        {
            ViewBag.Shippers = await _context.Shippers.OrderBy(s => s.ShipperName).ToListAsync();
            ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();

            Inbound model;
            if (id.HasValue && id.Value != Guid.Empty)
            {
                model = await _context.Inbounds.FirstOrDefaultAsync(i => i.InboundId == id.Value) ?? new Inbound();
            }
            else
            {
                model = new Inbound
                {
                    PalletCount = 0,
                    CaseCount = 1,
                    Status = 1,
                    InboundType = 1
                };
            }

            return View(model);
        }

        /// <summary>
        /// 入荷データを保存します。ステータスが「確認済（11）」の場合は自動的に在庫テーブル（t_inventory）へケース数分の在庫レコードを生成します。
        /// </summary>
        /// <param name="inbound">入荷入力データ</param>
        /// <returns>成功時一覧画面、失敗時登録画面</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Inbound inbound)
        {
            if (inbound.CaseCount <= 0)
            {
                ModelState.AddModelError("CaseCount", "ケース数は1以上を入力してください。");
            }
            if (inbound.PalletCount < 0)
            {
                ModelState.AddModelError("PalletCount", "パレット数は0以上を入力してください。");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Shippers = await _context.Shippers.OrderBy(s => s.ShipperName).ToListAsync();
                ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
                return View(inbound);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                bool isNotFound = false;
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    bool isNew = inbound.InboundId == Guid.Empty || !await _context.Inbounds.AnyAsync(i => i.InboundId == inbound.InboundId);
                    Inbound? dbInbound;

                    if (isNew)
                    {
                        if (inbound.InboundId == Guid.Empty)
                        {
                            inbound.InboundId = Guid.NewGuid();
                        }
                        dbInbound = inbound;
                        _context.Inbounds.Add(dbInbound);
                    }
                    else
                    {
                        dbInbound = await _context.Inbounds.FirstOrDefaultAsync(i => i.InboundId == inbound.InboundId);
                        if (dbInbound == null)
                        {
                            isNotFound = true;
                            return;
                        }

                        dbInbound.ShipperId = inbound.ShipperId;
                        dbInbound.WarehouseId = inbound.WarehouseId;
                        dbInbound.ProductId = inbound.ProductId;
                        dbInbound.ScheduledDate = inbound.ScheduledDate;
                        dbInbound.ActualDate = inbound.ActualDate;
                        dbInbound.ConfirmedDate = inbound.ConfirmedDate;
                        dbInbound.InboundType = inbound.InboundType;
                        dbInbound.PalletCount = inbound.PalletCount;
                        dbInbound.CaseCount = inbound.CaseCount;
                        dbInbound.Remarks = inbound.Remarks;
                        dbInbound.Status = inbound.Status;
                        _context.Inbounds.Update(dbInbound);
                    }

                    await _context.SaveChangesAsync();

                    // ステータスが確認済（11）の場合、ケース数分だけ在庫（t_inventory）レコードを生成
                    if (dbInbound.Status == 11)
                    {
                        int existingInvCount = await _context.Inventories.CountAsync(inv => inv.InboundId == dbInbound.InboundId);
                        int neededNewRows = dbInbound.CaseCount - existingInvCount;

                        if (neededNewRows > 0)
                        {
                            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == dbInbound.ProductId);
                            if (product != null)
                            {
                                for (int i = 0; i < neededNewRows; i++)
                                {
                                    var inv = new Inventory
                                    {
                                        InventoryId = Guid.NewGuid(),
                                        InboundId = dbInbound.InboundId,
                                        ShipperId = dbInbound.ShipperId,
                                        WarehouseId = dbInbound.WarehouseId,
                                        ProductId = dbInbound.ProductId,
                                        ActualInboundDate = dbInbound.ActualDate ?? DateTime.Now,
                                        ScheduledOutboundDate = null,
                                        ActualOutboundDate = null,
                                        CurrentQuantity = product.Quantity,
                                        IsLoose = false,
                                        Status = 1 // 1: 在庫あり
                                    };
                                    _context.Inventories.Add(inv);
                                }
                                await _context.SaveChangesAsync();
                            }
                        }
                    }

                    await transaction.CommitAsync();
                });

                if (isNotFound)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "入庫データを保存しました。";
                return RedirectToAction(nameof(List));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"エラーが発生しました: {ex.Message}");
                ViewBag.Shippers = await _context.Shippers.OrderBy(s => s.ShipperName).ToListAsync();
                ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
                return View(inbound);
            }
        }

        /// <summary>
        /// 入庫報告CSVファイルを読み込み、一括で入荷データおよび在庫データを登録します。
        /// </summary>
        /// <param name="shipperId">荷主ID</param>
        /// <param name="warehouseId">倉庫ID</param>
        /// <param name="csvFile">CSVファイル</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCsv(Guid shipperId, Guid warehouseId, IFormFile csvFile)
        {
            if (shipperId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "荷主を選択してください。";
                return RedirectToAction(nameof(List));
            }
            if (warehouseId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "倉庫を選択してください。";
                return RedirectToAction(nameof(List));
            }
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["ErrorMessage"] = "CSVファイルを選択してください。";
                return RedirectToAction(nameof(List));
            }

            try
            {
                var rows = await CsvService.ReadCsvAsync(csvFile);
                if (rows.Count <= 1)
                {
                    throw new Exception("取り込むデータが存在しません。");
                }

                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    var productsDict = await _context.Products.ToDictionaryAsync(p => p.ProductId, p => p);
                    var productIds = productsDict.Keys.ToList();

                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 6) continue;

                        string productId = (r[0] ?? "").Trim();
                        string actualDateStr = (r[1] ?? "").Trim();
                        string confirmedDateStr = (r[2] ?? "").Trim();
                        string typeStr = (r[3] ?? "").Trim();
                        string palletStr = (r[4] ?? "").Trim();
                        string caseStr = (r[5] ?? "").Trim();
                        string remarks = r.Length >= 7 ? r[6] : null;

                        if (string.IsNullOrWhiteSpace(productId))
                        {
                            throw new Exception($"{i + 1}行目: 商品IDが空です。");
                        }
                        if (!productIds.Contains(productId))
                        {
                            throw new Exception($"{i + 1}行目: 商品ID '{productId}' は商品マスタに存在しません。");
                        }

                        var currentProduct = productsDict[productId];

                        // 日付パース (yyyyMMdd)
                        if (!DateTime.TryParseExact(actualDateStr, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var actualDate))
                        {
                            throw new Exception($"{i + 1}行目: 入庫実績日のフォーマットが不正です。");
                        }

                        if (!DateTime.TryParseExact(confirmedDateStr, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var confirmedDate))
                        {
                            throw new Exception($"{i + 1}行目: 入庫確認日のフォーマットが不正です。");
                        }

                        int.TryParse(typeStr, out var inboundType);
                        if (inboundType < 1 || inboundType > 3) inboundType = 1;

                        int.TryParse(palletStr, out var palletCount);
                        int.TryParse(caseStr, out var caseCount);
                        if (caseCount <= 0)
                        {
                            throw new Exception($"{i + 1}行目: ケース数は1以上を指定してください。");
                        }

                        var inbound = new Inbound
                        {
                            InboundId = Guid.NewGuid(),
                            ShipperId = shipperId,
                            WarehouseId = warehouseId,
                            ProductId = productId,
                            ScheduledDate = actualDate,
                            ActualDate = actualDate,
                            ConfirmedDate = confirmedDate,
                            InboundType = inboundType,
                            PalletCount = palletCount,
                            CaseCount = caseCount,
                            Remarks = remarks,
                            Status = 11, // 11: 確認済
                            IsDeleted = false
                        };
                        _context.Inbounds.Add(inbound);

                        // 在庫（t_inventory）の自動作成
                        for (int k = 0; k < caseCount; k++)
                        {
                            var inv = new Inventory
                            {
                                InventoryId = Guid.NewGuid(),
                                InboundId = inbound.InboundId,
                                ShipperId = shipperId,
                                WarehouseId = warehouseId,
                                ProductId = productId,
                                ActualInboundDate = actualDate,
                                ScheduledOutboundDate = null,
                                ActualOutboundDate = null,
                                CurrentQuantity = currentProduct.Quantity,
                                IsLoose = false,
                                Status = 1
                            };
                            _context.Inventories.Add(inv);
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                TempData["SuccessMessage"] = "入庫報告CSVの取り込みおよび在庫反映が完了しました。";
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                TempData["ErrorMessage"] = $"取り込みエラー: {detail}";
            }

            return RedirectToAction(nameof(List));
        }
    }
}
