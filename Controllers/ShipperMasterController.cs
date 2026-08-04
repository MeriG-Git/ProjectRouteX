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
using RouteXWms.Helpers;

namespace RouteXWms.Controllers
{
    /// <summary>
    /// 荷主マスター（m_shipper）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class ShipperMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public ShipperMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 荷主マスター一覧画面を表示します。
        /// </summary>
        /// <param name="searchName">荷主名検索キーワード</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(string? searchName, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_Shipper";
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

            var query = _context.Shippers.IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(s => !s.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(s => s.ShipperName.Contains(searchName));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(s => s.ShipperName)
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
        /// 荷主マスターを登録または更新します。
        /// </summary>
        /// <param name="shipper">入力荷主モデル</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Shipper shipper)
        {
            if (shipper.ShipperId == Guid.Empty)
            {
                shipper.ShipperId = Guid.NewGuid();
                _context.Shippers.Add(shipper);
            }
            else
            {
                var existing = await _context.Shippers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ShipperId == shipper.ShipperId);
                if (existing != null)
                {
                    existing.ShipperName = shipper.ShipperName;
                    existing.ShipperAddress1 = shipper.ShipperAddress1;
                    existing.ShipperAddress2 = shipper.ShipperAddress2;
                    existing.ShipperTel = shipper.ShipperTel;
                    _context.Shippers.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 荷主を論理削除します。
        /// </summary>
        /// <param name="id">荷主ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.Shippers.FindAsync(id);
            if (item != null)
            {
                _context.Shippers.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された荷主を復元します。
        /// </summary>
        /// <param name="id">荷主ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var item = await _context.Shippers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ShipperId == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の荷主を一括削除します。
        /// </summary>
        /// <param name="ids">対象荷主ID配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(Guid[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.Shippers.Where(s => ids.Contains(s.ShipperId)).ToListAsync();
                _context.Shippers.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全荷主マスターをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.Shippers.ToListAsync();
            var headers = new[] { "荷主ID", "荷主名", "荷主住所1", "荷主住所2", "荷主電話番号" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, s => new[]
            {
                s.ShipperId.ToString(),
                s.ShipperName,
                s.ShipperAddress1,
                s.ShipperAddress2,
                s.ShipperTel
            });

            return File(bytes, "text/csv; charset=utf-8", "m_shipper.csv");
        }

        /// <summary>
        /// CSVファイルから荷主マスターを一括インポート（追加・更新）します。
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
                        if (r.Length < 5) continue;

                        string idStr = r[0];
                        string name = r[1];
                        string addr1 = r[2];
                        string addr2 = r[3];
                        string tel = r[4];

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newShipper = new Shipper
                            {
                                ShipperId = Guid.NewGuid(),
                                ShipperName = name,
                                ShipperAddress1 = addr1,
                                ShipperAddress2 = addr2,
                                ShipperTel = tel
                            };
                            _context.Shippers.Add(newShipper);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 荷主IDのフォーマットが不正です。({idStr})");
                            }
                            var existing = await _context.Shippers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ShipperId == id)
                                        ?? _context.Shippers.Local.FirstOrDefault(s => s.ShipperId == id);
                            if (existing == null)
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された荷主ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new Shipper
                                {
                                    ShipperId = id,
                                    ShipperName = name,
                                    ShipperAddress1 = addr1,
                                    ShipperAddress2 = addr2,
                                    ShipperTel = tel
                                };
                                _context.Shippers.Add(existing);
                            }
                            else
                            {
                                existing.ShipperName = name;
                                existing.ShipperAddress1 = addr1;
                                existing.ShipperAddress2 = addr2;
                                existing.ShipperTel = tel;
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
                TempData["ErrorMessage"] = $"インポートエラー: {ErrorHelper.ToUserFriendlyMessage(ex)}";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// SSE ストリーミングを用いて荷主マスターをリアルタイム進捗表示付きで高パフォーマンスに一括インポートします。
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
                    await sendProgressAsync(new { status = "phase", title = "【3/4】DBマスター事前照合中...", message = "既存の荷主マスター情報を一括照合キャッシュ中..." });
                    var existingMap = await _context.Shippers.IgnoreQueryFilters().ToDictionaryAsync(s => s.ShipperId);

                    // フェーズ4: データインポート・書き込み開始
                    await sendProgressAsync(new { status = "start", title = "【4/4】DB一括更新中...", current = 0, total = total, currentKey = "" });

                    int processedCount = 0;
                    int batchSize = 1000;

                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 2) continue;

                        string idStr = (r[0] ?? "").Trim();
                        string name = (r[1] ?? "").Trim();
                        string addr1 = r.Length > 2 ? (r[2] ?? "").Trim() : "";
                        string addr2 = r.Length > 3 ? (r[3] ?? "").Trim() : "";
                        string tel = r.Length > 4 ? (r[4] ?? "").Trim() : "";

                        if (string.IsNullOrWhiteSpace(name)) continue;

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newShipper = new Shipper
                            {
                                ShipperId = Guid.NewGuid(),
                                ShipperName = name,
                                ShipperAddress1 = addr1,
                                ShipperAddress2 = addr2,
                                ShipperTel = tel
                            };
                            _context.Shippers.Add(newShipper);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 荷主IDのフォーマットが不正です。({idStr})");
                            }

                            if (!existingMap.TryGetValue(id, out var existing))
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された荷主ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new Shipper
                                {
                                    ShipperId = id,
                                    ShipperName = name,
                                    ShipperAddress1 = addr1,
                                    ShipperAddress2 = addr2,
                                    ShipperTel = tel
                                };
                                _context.Shippers.Add(existing);
                                existingMap[id] = existing;
                            }
                            else
                            {
                                existing.ShipperName = name;
                                existing.ShipperAddress1 = addr1;
                                existing.ShipperAddress2 = addr2;
                                existing.ShipperTel = tel;
                                existing.IsDeleted = false;
                            }
                        }

                        processedCount++;

                        if (processedCount % 100 == 0 || i == rows.Count - 1)
                        {
                            double elapsedSec = stopwatch.Elapsed.TotalSeconds;
                            int speed = elapsedSec > 0 ? (int)(processedCount / elapsedSec) : processedCount;
                            await sendProgressAsync(new { 
                                status = "processing", 
                                title = "【4/4】DB一括更新中...",
                                current = processedCount, 
                                total = total, 
                                speed = speed,
                                elapsed = elapsedSec,
                                currentKey = $"荷主名: {name}" 
                            });
                        }

                        if (processedCount % batchSize == 0)
                        {
                            await _context.SaveChangesAsync();
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    stopwatch.Stop();
                    double totalSec = stopwatch.Elapsed.TotalSeconds;
                    await sendProgressAsync(new { 
                        status = "completed", 
                        current = processedCount, 
                        total = total, 
                        elapsed = totalSec,
                        message = $"荷主マスターのインポートが完了しました。（全 {processedCount:N0} 件 / 処理時間: {totalSec:F1}秒）" 
                    });
                });
            }
            catch (Exception ex)
            {
                await sendProgressAsync(new { status = "error", message = $"インポートエラー: {ErrorHelper.ToUserFriendlyMessage(ex)}" });
            }
        }
    }
}
