using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RouteXWms.Data;

namespace RouteXWms.Controllers
{
    /// <summary>
    /// ホーム・ダッシュボード表示用コントローラー
    /// サマリー件数（荷主数、倉庫数、商品数、入荷予定数、出荷予定数、在庫データ数等）の取得を行います。
    /// </summary>
    public class HomeController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public HomeController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// システムのダッシュボード画面を表示します。
        /// 各種マスターおよび業務データの件数を集計してViewBagに設定します。
        /// </summary>
        /// <returns>ダッシュボードビュー</returns>
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalShippers = await _context.Shippers.CountAsync();
            ViewBag.TotalWarehouses = await _context.Warehouses.CountAsync();
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.PendingInbound = await _context.Inbounds.CountAsync(i => i.Status == 1);
            ViewBag.PendingOutbound = await _context.Outbounds.CountAsync(o => o.Status == 1);
            ViewBag.TotalInventory = await _context.Inventories.CountAsync(i => i.Status == 1);

            // 診断ログ出力（マスター整合性チェック）
            System.Console.WriteLine("======= DIAGNOSTIC LOG START =======");
            var carrierList = await _context.Carriers.ToListAsync();
            foreach (var c in carrierList)
            {
                System.Console.WriteLine($"Carrier: ID={c.CarrierId}, Name={c.CarrierName}");
            }
            var dTables = await _context.FreightTables.ToListAsync();
            foreach (var dt in dTables)
            {
                System.Console.WriteLine($"FreightTable: ID={dt.FreightTableId}, Name={dt.RateName}, RateTableType={dt.RateTableType}, CarrierId={dt.CarrierId}");
            }
            var scs = await _context.ShippingClasses.ToListAsync();
            foreach (var sc in scs)
            {
                System.Console.WriteLine($"ShippingClass: ID={sc.ShippingClassId}, Name={sc.ClassName}, RateTableType={sc.RateTableType}, CarrierId={sc.CarrierId}");
            }
            var wdrs = await _context.WarehouseDistanceRates.Include(w => w.FreightTable).ToListAsync();
            foreach (var wdr in wdrs)
            {
                System.Console.WriteLine($"WarehouseDistanceRate: WarehouseId={wdr.WarehouseId}, FreightTableId={wdr.FreightTableId}, CarrierId={wdr.FreightTable?.CarrierId}");
            }
            System.Console.WriteLine("======= DIAGNOSTIC LOG END =======");

            return View();
        }
    }
}
