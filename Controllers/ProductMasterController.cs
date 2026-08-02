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
    /// 商品マスター（m_product）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class ProductMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public ProductMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 商品マスター一覧画面を表示します。
        /// </summary>
        /// <param name="searchName">商品名・コード検索文字列</param>
        /// <param name="showDeleted">削除済みレコード表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>商品マスター一覧ビュー</returns>
        public async Task<IActionResult> Index(string? searchName, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_Product";
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

            var query = _context.Products.IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(p => !p.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(p => p.ProductName.Contains(searchName) || p.ProductId.Contains(searchName));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(p => p.ProductId)
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
        /// 商品検索オートコンプリート用のJSONデータを返却します。
        /// </summary>
        /// <param name="term">検索キーワード（コード・名称・JAN）</param>
        /// <returns>商品情報のJSON配列</returns>
        [HttpGet]
        public async Task<IActionResult> SearchJson(string term)
        {
            var query = _context.Products.AsQueryable();
            if (!string.IsNullOrWhiteSpace(term))
            {
                query = query.Where(p => p.ProductId.Contains(term) || p.ProductName.Contains(term) || p.JanCode.Contains(term));
            }

            var products = await query.Take(20).Select(p => new
            {
                productId = p.ProductId,
                productName = p.ProductName,
                janCode = p.JanCode,
                quantity = p.Quantity,
                weight = p.Weight
            }).ToListAsync();

            return Json(products);
        }

        /// <summary>
        /// 商品マスターの新規登録または更新保存を行います。
        /// </summary>
        /// <param name="product">入力商品モデル</param>
        /// <param name="isNew">新規追加フラグ</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Product product, bool isNew)
        {
            if (isNew)
            {
                var existing = await _context.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProductId == product.ProductId);
                if (existing != null)
                {
                    TempData["ErrorMessage"] = "指定された商品IDは既に存在します。";
                    return RedirectToAction(nameof(Index));
                }
                _context.Products.Add(product);
            }
            else
            {
                var existing = await _context.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProductId == product.ProductId);
                if (existing != null)
                {
                    existing.ProductName = product.ProductName;
                    existing.JanCode = product.JanCode;
                    existing.Length = product.Length;
                    existing.Width = product.Width;
                    existing.Height = product.Height;
                    existing.Weight = product.Weight;
                    existing.Quantity = product.Quantity;
                    _context.Products.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 指定された商品を論理削除します。
        /// </summary>
        /// <param name="id">商品コード</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var item = await _context.Products.FindAsync(id);
            if (item != null)
            {
                _context.Products.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された商品を復元します。
        /// </summary>
        /// <param name="id">商品コード</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            var item = await _context.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProductId == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の商品を一括で論理削除します。
        /// </summary>
        /// <param name="ids">一括削除対象の商品コード配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(string[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.Products.Where(p => ids.Contains(p.ProductId)).ToListAsync();
                _context.Products.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全商品マスターをBOM付きUTF-8形式のCSVファイルとしてダウンロード出力します。
        /// </summary>
        /// <returns>CSVファイルレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.Products.ToListAsync();
            var headers = new[] { "商品コード", "商品名", "JANコード", "縦(cm)", "横(cm)", "高さ(cm)", "重量(kg)", "ケース入数" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, p => new[]
            {
                p.ProductId,
                p.ProductName,
                p.JanCode,
                p.Length.ToString(),
                p.Width.ToString(),
                p.Height.ToString(),
                p.Weight.ToString(),
                p.Quantity.ToString()
            });

            return File(bytes, "text/csv; charset=utf-8", "m_product.csv");
        }

        /// <summary>
        /// CSVファイルから商品マスターを一括インポート（追加・更新）します。
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
                    if (r.Length < 8) continue;

                    string id = r[0];
                    string name = r[1];
                    string jan = r[2];
                    decimal.TryParse(r[3], out var len);
                    decimal.TryParse(r[4], out var wid);
                    decimal.TryParse(r[5], out var hei);
                    int.TryParse(r[6], out var wgt);
                    int.TryParse(r[7], out var qty);

                    var existing = await _context.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProductId == id)
                                ?? _context.Products.Local.FirstOrDefault(p => p.ProductId == id);
                    if (existing == null)
                    {
                        if (!createIfNotFound)
                        {
                            throw new Exception($"{i + 1}行目: 指定された商品コード ({id}) のレコードが存在しません。");
                        }
                        var newProduct = new Product
                        {
                            ProductId = id,
                            ProductName = name,
                            JanCode = jan,
                            Length = len,
                            Width = wid,
                            Height = hei,
                            Weight = wgt,
                            Quantity = qty
                        };
                        _context.Products.Add(newProduct);
                    }
                    else
                    {
                        existing.ProductName = name;
                        existing.JanCode = jan;
                        existing.Length = len;
                        existing.Width = wid;
                        existing.Height = hei;
                        existing.Weight = wgt;
                        existing.Quantity = qty;
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
