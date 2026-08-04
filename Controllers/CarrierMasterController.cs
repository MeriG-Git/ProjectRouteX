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
    /// 運送会社マスター（m_carrier）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class CarrierMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public CarrierMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 運送会社マスター一覧画面を表示します。
        /// </summary>
        /// <param name="searchName">運送会社名検索キーワード</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(string? searchName, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_Carrier";
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

            var query = _context.Carriers.IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(c => !c.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(c => c.CarrierName.Contains(searchName));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(c => c.CarrierName)
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
        /// 運送会社マスターを登録または更新します。
        /// </summary>
        /// <param name="carrier">入力運送会社モデル</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Carrier carrier)
        {
            if (carrier.CarrierId == Guid.Empty)
            {
                carrier.CarrierId = Guid.NewGuid();
                _context.Carriers.Add(carrier);
            }
            else
            {
                var existing = await _context.Carriers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CarrierId == carrier.CarrierId);
                if (existing != null)
                {
                    existing.CarrierName = carrier.CarrierName;
                    _context.Carriers.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 運送会社を論理削除します。
        /// </summary>
        /// <param name="id">運送会社ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.Carriers.FindAsync(id);
            if (item != null)
            {
                _context.Carriers.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された運送会社を復元します。
        /// </summary>
        /// <param name="id">運送会社ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var item = await _context.Carriers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CarrierId == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の運送会社を一括削除します。
        /// </summary>
        /// <param name="ids">対象運送会社ID配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(Guid[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.Carriers.Where(c => ids.Contains(c.CarrierId)).ToListAsync();
                _context.Carriers.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全運送会社マスターをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.Carriers.ToListAsync();
            var headers = new[] { "運送会社ID", "運送会社名" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, c => new[]
            {
                c.CarrierId.ToString(),
                c.CarrierName
            });

            return File(bytes, "text/csv; charset=utf-8", "m_carrier.csv");
        }

        /// <summary>
        /// CSVファイルから運送会社マスターを一括インポート（追加・更新）します。
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
                        if (r.Length < 2) continue;

                        string idStr = r[0];
                        string name = r[1];

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newCarrier = new Carrier
                            {
                                CarrierId = Guid.NewGuid(),
                                CarrierName = name
                            };
                            _context.Carriers.Add(newCarrier);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 運送会社IDのフォーマットが不正です。({idStr})");
                            }
                            var existing = await _context.Carriers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CarrierId == id)
                                        ?? _context.Carriers.Local.FirstOrDefault(c => c.CarrierId == id);
                            if (existing == null)
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された運送会社ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new Carrier
                                {
                                    CarrierId = id,
                                    CarrierName = name
                                };
                                _context.Carriers.Add(existing);
                            }
                            else
                            {
                                existing.CarrierName = name;
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
        /// SSE ストリーミングを用いて運送会社マスターをリアルタイム進捗表示付きで高パフォーマンスに一括インポートします。
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
                    await sendProgressAsync(new { status = "phase", title = "【3/4】DBマスター事前照合中...", message = "既存の運送会社マスター情報を一括照合キャッシュ中..." });
                    var existingMap = await _context.Carriers.IgnoreQueryFilters().ToDictionaryAsync(c => c.CarrierId);

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

                        if (string.IsNullOrWhiteSpace(name)) continue;

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newCarrier = new Carrier
                            {
                                CarrierId = Guid.NewGuid(),
                                CarrierName = name
                            };
                            _context.Carriers.Add(newCarrier);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 運送会社IDのフォーマットが不正です。({idStr})");
                            }

                            if (!existingMap.TryGetValue(id, out var existing))
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された運送会社ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new Carrier
                                {
                                    CarrierId = id,
                                    CarrierName = name
                                };
                                _context.Carriers.Add(existing);
                                existingMap[id] = existing;
                            }
                            else
                            {
                                existing.CarrierName = name;
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
                                currentKey = $"運送会社名: {name}" 
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
                        message = $"運送会社マスターのインポートが完了しました。（全 {processedCount:N0} 件 / 処理時間: {totalSec:F1}秒）" 
                    });
                });
            }
            catch (Exception ex)
            {
                await sendProgressAsync(new { status = "error", message = $"インポートエラー: {RouteXWms.Helpers.ErrorHelper.ToUserFriendlyMessage(ex)}" });
            }
        }
    }
}
