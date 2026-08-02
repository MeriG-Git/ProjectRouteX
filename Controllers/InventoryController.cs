using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RouteXWms.Data;
using RouteXWms.Models;

namespace RouteXWms.Controllers
{
    /// <summary>
    /// 在庫情報の照会・集計・調整を行うコントローラー
    /// </summary>
    public class InventoryController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public InventoryController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 在庫一覧画面を表示します。
        /// 荷主、倉庫、商品、ステータスによる絞り込みおよびページネーションに対応します。
        /// </summary>
        /// <param name="shipperId">検索対象荷主ID</param>
        /// <param name="warehouseId">検索対象倉庫ID</param>
        /// <param name="productId">検索対象商品コード</param>
        /// <param name="status">検索対象ステータス</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの件数</param>
        /// <returns>在庫一覧ビュー</returns>
        [HttpGet]
        public async Task<IActionResult> List(Guid? shipperId, Guid? warehouseId, string? productId, int? status, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Inventory_List";
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

            var query = _context.Inventories
                .Include(i => i.Shipper)
                .Include(i => i.Warehouse)
                .Include(i => i.Product)
                .AsQueryable();

            if (shipperId.HasValue && shipperId.Value != Guid.Empty)
            {
                query = query.Where(i => i.ShipperId == shipperId.Value);
            }
            if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
            {
                query = query.Where(i => i.WarehouseId == warehouseId.Value);
            }
            if (!string.IsNullOrWhiteSpace(productId))
            {
                query = query.Where(i => i.ProductId.Contains(productId));
            }
            if (status.HasValue && status.Value > 0)
            {
                query = query.Where(i => i.Status == status.Value);
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(i => i.WarehouseId)
                .ThenBy(i => i.ProductId)
                .ThenBy(i => i.ActualInboundDate)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.Shippers = await _context.Shippers.OrderBy(s => s.ShipperName).ToListAsync();
            ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
            ViewBag.SelectedShipper = shipperId;
            ViewBag.SelectedWarehouse = warehouseId;
            ViewBag.ProductId = productId;
            ViewBag.Status = status;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 商品・倉庫・荷主別の在庫集計サマリー画面を表示します。
        /// </summary>
        /// <param name="shipperId">荷主ID</param>
        /// <param name="warehouseId">倉庫ID</param>
        /// <param name="productId">商品コード</param>
        /// <returns>在庫サマリービュー</returns>
        [HttpGet]
        public async Task<IActionResult> Summary(Guid? shipperId, Guid? warehouseId, string? productId)
        {
            var query = _context.Inventories.Where(i => i.Status != 21).AsQueryable();

            if (shipperId.HasValue && shipperId.Value != Guid.Empty)
            {
                query = query.Where(i => i.ShipperId == shipperId.Value);
            }
            if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
            {
                query = query.Where(i => i.WarehouseId == warehouseId.Value);
            }
            if (!string.IsNullOrWhiteSpace(productId))
            {
                query = query.Where(i => i.ProductId.Contains(productId));
            }

            var summary = await query
                .Include(i => i.Warehouse)
                .Include(i => i.Product)
                .Include(i => i.Shipper)
                .GroupBy(i => new { i.WarehouseId, i.ShipperId, i.ProductId, ProductQuantity = i.Product != null ? i.Product.Quantity : 1 })
                .Select(g => new InventorySummaryItem
                {
                    WarehouseName = g.First().Warehouse != null ? g.First().Warehouse!.WarehouseName : "未設定",
                    ShipperName = g.First().Shipper != null ? g.First().Shipper!.ShipperName : "未設定",
                    ProductId = g.Key.ProductId,
                    ProductName = g.First().Product != null ? g.First().Product!.ProductName : g.Key.ProductId,
                    InStockCount = g.Count(i => i.Status == 1 && i.CurrentQuantity > 0),
                    InStockPieces = g.Where(i => i.Status == 1).Sum(i => i.CurrentQuantity),
                    ReservedCount = g.Count(i => i.Status == 11),
                    TotalCount = g.Count(i => (i.Status == 1 && i.CurrentQuantity > 0) || i.Status == 11)
                })
                .OrderBy(s => s.WarehouseName)
                .ThenBy(s => s.ProductName)
                .ToListAsync();

            ViewBag.Shippers = await _context.Shippers.OrderBy(s => s.ShipperName).ToListAsync();
            ViewBag.Warehouses = await _context.Warehouses.OrderBy(w => w.WarehouseName).ToListAsync();
            ViewBag.SelectedShipper = shipperId;
            ViewBag.SelectedWarehouse = warehouseId;
            ViewBag.ProductId = productId;

            return View(summary);
        }
    }

    /// <summary>
    /// 在庫集計サマリー表示用モデル
    /// </summary>
    public class InventorySummaryItem
    {
        /// <summary>倉庫名</summary>
        public string WarehouseName { get; set; } = string.Empty;
        /// <summary>荷主名</summary>
        public string ShipperName { get; set; } = string.Empty;
        /// <summary>商品コード</summary>
        public string ProductId { get; set; } = string.Empty;
        /// <summary>商品名</summary>
        public string ProductName { get; set; } = string.Empty;
        /// <summary>保管中箱数</summary>
        public int InStockCount { get; set; }
        /// <summary>保管中総ピース数</summary>
        public int InStockPieces { get; set; }
        /// <summary>出荷引当済箱数</summary>
        public int ReservedCount { get; set; }
        /// <summary>合計管理箱数</summary>
        public int TotalCount { get; set; }
    }
}
