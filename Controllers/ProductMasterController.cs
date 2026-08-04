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
        /// SSE ストリーミングを用いて商品マスターをリアルタイム進捗表示付きで高パフォーマンスに一括インポートします。
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

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // フェーズ1: ファイル読み込み
                await sendProgressAsync(new { status = "phase", title = "【1/4】ファイル読み込み中...", message = $"ファイル名: {csvFile.FileName} ({csvFile.Length:N0} bytes) を解析中..." });
                var rows = await CsvService.ReadCsvAsync(csvFile);

                // フェーズ2: 件数・構造検証
                int total = rows.Count - 1;
                await sendProgressAsync(new { status = "phase", title = "【2/4】件数チェック・構文検証中...", message = $"総データ件数: {total:N0} 件 の構文を検証中..." });

                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    // フェーズ3: DB事前検証
                    await sendProgressAsync(new { status = "phase", title = "【3/4】DBマスター事前照合中...", message = "既存の商品コード・マスター情報を一括照合キャッシュ中..." });
                    var existingMap = await _context.Products.IgnoreQueryFilters().ToDictionaryAsync(p => p.ProductId);

                    // フェーズ4: データインポート・書き込み開始
                    await sendProgressAsync(new { status = "start", title = "【4/4】DB一括更新中...", current = 0, total = total, currentKey = "" });

                    int processedCount = 0;
                    int batchSize = 1000;

                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 8) continue;

                        string id = (r[0] ?? "").Trim();
                        string name = (r[1] ?? "").Trim();
                        string jan = (r[2] ?? "").Trim();
                        decimal.TryParse(r[3], out var len);
                        decimal.TryParse(r[4], out var wid);
                        decimal.TryParse(r[5], out var hei);
                        int.TryParse(r[6], out var wgt);
                        int.TryParse(r[7], out var qty);

                        if (string.IsNullOrWhiteSpace(id)) continue;

                        if (!existingMap.TryGetValue(id, out var existing))
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
                            existingMap[id] = newProduct;
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

                        processedCount++;

                        if (processedCount % 100 == 0 || i == rows.Count - 1)
                        {
                            await sendProgressAsync(new { status = "processing", current = processedCount, total = total, currentKey = $"{name} ({id})" });
                        }

                        if (processedCount % batchSize == 0)
                        {
                            await _context.SaveChangesAsync();
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await sendProgressAsync(new { status = "completed", current = processedCount, total = total, message = $"商品マスターのインポートが完了しました。（全 {processedCount:N0} 件）" });
                });
            }
            catch (Exception ex)
            {
                await sendProgressAsync(new { status = "error", message = $"インポートエラー: {RouteXWms.Helpers.ErrorHelper.ToUserFriendlyMessage(ex)}" });
            }
        }
    }
}
