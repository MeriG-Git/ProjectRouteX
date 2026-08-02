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
    /// 出荷区分マスター（m_shipping_class）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class ShippingClassMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public ShippingClassMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 出荷区分マスター一覧画面を表示します。
        /// </summary>
        /// <param name="searchName">区分名検索キーワード</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(string? searchName, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_ShippingClass";
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

            var query = _context.ShippingClasses.Include(s => s.Carrier).IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(s => !s.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(s => s.ClassName.Contains(searchName));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(s => s.ClassName)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.Carriers = await _context.Carriers.OrderBy(c => c.CarrierName).ToListAsync();
            ViewBag.SearchName = searchName;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 出荷区分マスターを登録または更新します。
        /// </summary>
        /// <param name="shippingClass">入力モデル</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(ShippingClass shippingClass)
        {
            if (shippingClass.ShippingClassId == Guid.Empty)
            {
                shippingClass.ShippingClassId = Guid.NewGuid();
                _context.ShippingClasses.Add(shippingClass);
            }
            else
            {
                var existing = await _context.ShippingClasses.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ShippingClassId == shippingClass.ShippingClassId);
                if (existing != null)
                {
                    existing.CarrierId = shippingClass.CarrierId;
                    existing.ClassName = shippingClass.ClassName;
                    existing.RateTableType = shippingClass.RateTableType;
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 出荷区分を論理削除します。
        /// </summary>
        /// <param name="id">出荷区分ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.ShippingClasses.FindAsync(id);
            if (item != null)
            {
                _context.ShippingClasses.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された出荷区分を復元します。
        /// </summary>
        /// <param name="id">出荷区分ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var item = await _context.ShippingClasses.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ShippingClassId == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の出荷区分を一括削除します。
        /// </summary>
        /// <param name="ids">対象出荷区分ID配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(Guid[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.ShippingClasses.Where(s => ids.Contains(s.ShippingClassId)).ToListAsync();
                _context.ShippingClasses.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全出荷区分マスターをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.ShippingClasses.Include(s => s.Carrier).ToListAsync();
            var headers = new[] { "出庫区分ID", "運送会社ID", "運送会社名", "料金表種別", "区分名" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, s => new[]
            {
                s.ShippingClassId.ToString(),
                s.CarrierId.ToString(),
                s.Carrier?.CarrierName ?? "",
                s.RateTableType.ToString(),
                s.ClassName
            });

            return File(bytes, "text/csv; charset=utf-8", "m_shipping_class.csv");
        }

        /// <summary>
        /// CSVファイルから出荷区分マスターを一括インポート（追加・更新）します。
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
                    if (r.Length < 3) continue;

                    string idStr = r[0];
                    if (!Guid.TryParse(r[1], out var carrierId)) continue;

                    int rateTableType = 1;
                    string name = "";

                    if (r.Length == 4)
                    {
                        name = r[3];
                    }
                    else if (r.Length >= 5)
                    {
                        if (int.TryParse(r[3], out var val))
                        {
                            rateTableType = val;
                        }
                        name = r[4];
                    }
                    else
                    {
                        name = r[2];
                    }

                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (string.IsNullOrWhiteSpace(idStr))
                    {
                        var newClass = new ShippingClass
                        {
                            ShippingClassId = Guid.NewGuid(),
                            CarrierId = carrierId,
                            RateTableType = rateTableType,
                            ClassName = name
                        };
                        _context.ShippingClasses.Add(newClass);
                    }
                    else
                    {
                        if (!Guid.TryParse(idStr, out var id))
                        {
                            throw new Exception($"{i + 1}行目: 出庫区分IDのフォーマットが不正です。");
                        }
                        var existing = await _context.ShippingClasses.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ShippingClassId == id)
                                    ?? _context.ShippingClasses.Local.FirstOrDefault(s => s.ShippingClassId == id);
                        if (existing == null)
                        {
                            if (!createIfNotFound)
                            {
                                throw new Exception($"{i + 1}行目: 指定された出庫区分ID ({idStr}) のレコードが存在しません。");
                            }
                            existing = new ShippingClass
                            {
                                ShippingClassId = id,
                                CarrierId = carrierId,
                                RateTableType = rateTableType,
                                ClassName = name
                            };
                            _context.ShippingClasses.Add(existing);
                        }
                        else
                        {
                            existing.CarrierId = carrierId;
                            existing.RateTableType = rateTableType;
                            existing.ClassName = name;
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
