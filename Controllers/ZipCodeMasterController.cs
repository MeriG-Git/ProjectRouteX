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
    /// 郵便番号マスター（m_zip_code）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// 大容量CSV（全国郵便番号データ）のプログレス表示付きリアルタイムストリーミングインポートに対応します。
    /// </summary>
    public class ZipCodeMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public ZipCodeMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 郵便番号マスター一覧画面を表示します。
        /// </summary>
        /// <param name="searchZip">郵便番号検索キーワード</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(string? searchZip, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_ZipCode";
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

            var query = _context.ZipCodes.IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(z => !z.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(searchZip))
            {
                query = query.Where(z => z.ZipCodeValue.Contains(searchZip));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(z => z.ZipCodeValue)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.SearchZip = searchZip;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 郵便番号マスターを登録または更新します。
        /// </summary>
        /// <param name="zipCode">入力モデル</param>
        /// <param name="isNew">新規追加フラグ</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(ZipCode zipCode, bool isNew)
        {
            if (isNew)
            {
                var existing = await _context.ZipCodes.IgnoreQueryFilters().FirstOrDefaultAsync(z => z.ZipCodeValue == zipCode.ZipCodeValue);
                if (existing != null)
                {
                    TempData["ErrorMessage"] = "指定された郵便番号は既に存在します。";
                    return RedirectToAction(nameof(Index));
                }
                _context.ZipCodes.Add(zipCode);
            }
            else
            {
                var existing = await _context.ZipCodes.IgnoreQueryFilters().FirstOrDefaultAsync(z => z.ZipCodeValue == zipCode.ZipCodeValue);
                if (existing != null)
                {
                    existing.PrefCode = zipCode.PrefCode;
                    existing.CityCode = zipCode.CityCode;
                    _context.ZipCodes.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 郵便番号マスターを論理削除します。
        /// </summary>
        /// <param name="id">郵便番号</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var item = await _context.ZipCodes.FindAsync(id);
            if (item != null)
            {
                _context.ZipCodes.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された郵便番号マスターを復元します。
        /// </summary>
        /// <param name="id">郵便番号</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            var item = await _context.ZipCodes.IgnoreQueryFilters().FirstOrDefaultAsync(z => z.ZipCodeValue == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の郵便番号マスターを一括削除します。
        /// </summary>
        /// <param name="ids">対象郵便番号文字列の配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(string[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.ZipCodes.Where(z => ids.Contains(z.ZipCodeValue)).ToListAsync();
                _context.ZipCodes.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全郵便番号マスターをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.ZipCodes.ToListAsync();
            var headers = new[] { "郵便番号", "都道府県コード", "市区町村コード" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, z => new[]
            {
                z.ZipCodeValue,
                z.PrefCode,
                z.CityCode
            });

            return File(bytes, "text/csv; charset=utf-8", "m_zip_code.csv");
        }

        /// <summary>
        /// CSVファイルから郵便番号マスターを一括インポート（通常リダイレクト処理）します。
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
                    var existingMap = await _context.ZipCodes.IgnoreQueryFilters().ToDictionaryAsync(z => z.ZipCodeValue);
                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 3) continue;

                        string zip = (r[0] ?? "").Replace("-", "").Trim();
                        string pref = (r[1] ?? "").Trim();
                        string city = (r[2] ?? "").Trim();

                        if (string.IsNullOrWhiteSpace(zip)) continue;

                        if (!existingMap.TryGetValue(zip, out var existing))
                        {
                            if (!createIfNotFound)
                            {
                                throw new Exception($"{i + 1}行目: 指定された郵便番号 ({zip}) のレコードが存在しません。");
                            }
                            var newZip = new ZipCode
                            {
                                ZipCodeValue = zip,
                                PrefCode = pref,
                                CityCode = city
                            };
                            _context.ZipCodes.Add(newZip);
                            existingMap[zip] = newZip;
                        }
                        else
                        {
                            existing.PrefCode = pref;
                            existing.CityCode = city;
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
        /// Server-Sent Events (SSE) を用いて大容量CSV（郵便番号データ）をプログレス通知付きでストリーミングインポートします。
        /// </summary>
        /// <param name="csvFile">CSVファイル</param>
        /// <param name="createIfNotFound">未存在時新規作成フラグ</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task ImportCsvStream(IFormFile csvFile, bool createIfNotFound = false)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

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
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    var existingMap = await _context.ZipCodes.IgnoreQueryFilters().ToDictionaryAsync(z => z.ZipCodeValue);
                    int total = rows.Count - 1;
                    await sendProgressAsync(new { status = "start", current = 0, total = total, currentKey = "" });

                    int processedCount = 0;
                    int batchSize = 1000;

                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 3) continue;

                        string zip = (r[0] ?? "").Replace("-", "").Trim();
                        string pref = (r[1] ?? "").Trim();
                        string city = (r[2] ?? "").Trim();

                        if (string.IsNullOrWhiteSpace(zip)) continue;

                        if (!existingMap.TryGetValue(zip, out var existing))
                        {
                            if (!createIfNotFound)
                            {
                                throw new Exception($"{i + 1}行目: 指定された郵便番号 ({zip}) のレコードが存在しません。");
                            }
                            var newZip = new ZipCode
                            {
                                ZipCodeValue = zip,
                                PrefCode = pref,
                                CityCode = city
                            };
                            _context.ZipCodes.Add(newZip);
                            existingMap[zip] = newZip;
                        }
                        else
                        {
                            existing.PrefCode = pref;
                            existing.CityCode = city;
                            existing.IsDeleted = false;
                        }

                        processedCount++;

                        if (processedCount % 100 == 0 || i == rows.Count - 1)
                        {
                            await sendProgressAsync(new { status = "processing", current = processedCount, total = total, currentKey = zip });
                        }

                        if (processedCount % batchSize == 0)
                        {
                            await _context.SaveChangesAsync();
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await sendProgressAsync(new { status = "completed", current = processedCount, total = total, message = $"CSVのインポートが完了しました。（全 {processedCount:N0} 件）" });
                });
            }
            catch (Exception ex)
            {
                await sendProgressAsync(new { status = "error", message = $"インポートエラー: {RouteXWms.Helpers.ErrorHelper.ToUserFriendlyMessage(ex)}" });
            }
        }
    }
}
