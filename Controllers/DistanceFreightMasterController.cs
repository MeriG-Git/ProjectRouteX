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
    /// 距離別運賃マスター（m_distance_freight）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class DistanceFreightMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public DistanceFreightMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 距離別運賃マスター一覧画面を表示します。
        /// </summary>
        /// <param name="freightTableId">運賃表ID絞り込み条件</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <param name="openId">特定レコードハイライト用ID</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(Guid? freightTableId, bool showDeleted = false, int page = 1, int? pageSize = null, Guid? openId = null)
        {
            const string cookieKey = "PageSize_Master_DistanceFreight";
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

            var query = _context.DistanceFreights.Include(d => d.FreightTable).IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(d => !d.IsDeleted);
            }

            if (freightTableId.HasValue && freightTableId.Value != Guid.Empty)
            {
                query = query.Where(d => d.FreightTableId == freightTableId.Value);
            }

            if (openId.HasValue && openId.Value != Guid.Empty)
            {
                query = query.Where(d => d.FreightId == openId.Value);
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(d => d.Size)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.FreightTables = await _context.FreightTables.OrderBy(t => t.RateName).ToListAsync();
            ViewBag.SelectedRateId = freightTableId;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);
            ViewBag.OpenId = openId;

            return View(items);
        }

        /// <summary>
        /// 距離別運賃マスターを登録または更新します。
        /// </summary>
        /// <param name="freight">入力運賃モデル</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(DistanceFreight freight)
        {
            if (freight.FreightId == Guid.Empty)
            {
                freight.FreightId = Guid.NewGuid();
                _context.DistanceFreights.Add(freight);
            }
            else
            {
                var existing = await _context.DistanceFreights.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.FreightId == freight.FreightId);
                if (existing != null)
                {
                    existing.FreightTableId = freight.FreightTableId;
                    existing.DistanceKm = freight.DistanceKm;
                    existing.Size = freight.Size;
                    existing.Cost = freight.Cost;
                    existing.Price = freight.Price;
                    existing.IsDeleted = false;
                    _context.DistanceFreights.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 距離別運賃マスターを論理削除します。
        /// </summary>
        /// <param name="id">運賃ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.DistanceFreights.FindAsync(id);
            if (item != null)
            {
                _context.DistanceFreights.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された距離別運賃マスターを復元します。
        /// </summary>
        /// <param name="id">運賃ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var item = await _context.DistanceFreights.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.FreightId == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の距離別運賃マスターを一括削除します。
        /// </summary>
        /// <param name="ids">対象運賃ID配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(Guid[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.DistanceFreights.Where(d => ids.Contains(d.FreightId)).ToListAsync();
                _context.DistanceFreights.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全距離別運賃マスターをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.DistanceFreights.ToListAsync();
            var headers = new[] { "運賃ID", "運賃表ID", "距離(km)", "サイズ", "原価", "売価" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, f => new[]
            {
                f.FreightId.ToString(),
                f.FreightTableId.ToString(),
                f.DistanceKm.ToString(),
                f.Size.ToString(),
                f.Cost.ToString(),
                f.Price.ToString()
            });

            return File(bytes, "text/csv; charset=utf-8", "m_distance_freight.csv");
        }

        /// <summary>
        /// CSVファイルから距離別運賃マスターを一括インポート（追加・更新）します。
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
                        if (r.Length < 6) continue;

                        string idStr = r[0];
                        if (!Guid.TryParse(r[1], out var rateId)) continue;
                        int.TryParse(r[2], out var dist);
                        int.TryParse(r[3], out var size);
                        int.TryParse(r[4], out var cost);
                        int.TryParse(r[5], out var price);

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newFreight = new DistanceFreight
                            {
                                FreightId = Guid.NewGuid(),
                                FreightTableId = rateId,
                                DistanceKm = dist,
                                Size = size,
                                Cost = cost,
                                Price = price
                            };
                            _context.DistanceFreights.Add(newFreight);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 運賃IDのフォーマットが不正です。({idStr})");
                            }
                            var existing = await _context.DistanceFreights.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.FreightId == id)
                                        ?? _context.DistanceFreights.Local.FirstOrDefault(d => d.FreightId == id);
                            if (existing == null)
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された運賃ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new DistanceFreight
                                {
                                    FreightId = id,
                                    FreightTableId = rateId,
                                    DistanceKm = dist,
                                    Size = size,
                                    Cost = cost,
                                    Price = price
                                };
                                _context.DistanceFreights.Add(existing);
                            }
                            else
                            {
                                existing.FreightTableId = rateId;
                                existing.DistanceKm = dist;
                                existing.Size = size;
                                existing.Cost = cost;
                                existing.Price = price;
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
        /// SSE ストリーミングを用いて輸送運賃マスターをリアルタイム進捗表示付きで高パフォーマンスに一括インポートします。
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task ImportCsvStream(IFormFile csvFile, bool createIfNotFound = false, Guid? defaultFreightTableId = null)
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

                // 事前検証（DBトランザクション開始前）
                if (defaultFreightTableId.HasValue && defaultFreightTableId.Value != Guid.Empty)
                {
                    bool defaultExists = await _context.FreightTables.AnyAsync(f => f.FreightTableId == defaultFreightTableId.Value && !f.IsDeleted);
                    if (!defaultExists)
                    {
                        await sendProgressAsync(new { status = "error", message = "指定されたデフォルト料金表がマスターに存在しません。" });
                        return;
                    }
                }
                else
                {
                    bool hasEmptyRateKey = false;
                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 3) continue;
                        string keyCol = (r.Length >= 2 && Guid.TryParse(r[1], out _)) ? r[1] : ((r.Length >= 1 && Guid.TryParse(r[0], out _)) ? r[0] : "");
                        if (string.IsNullOrWhiteSpace(keyCol))
                        {
                            hasEmptyRateKey = true;
                            break;
                        }
                    }

                    if (hasEmptyRateKey)
                    {
                        await sendProgressAsync(new { status = "need_selection", missing = new[] { "freightTable" }, message = "料金表IDが未指定のデータが検出されました。適用する料金表を選択してください。" });
                        return;
                    }
                }

                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    // フェーズ3: DB事前検証
                    await sendProgressAsync(new { status = "phase", title = "【3/4】DBマスター事前照合中...", message = "既存の運賃設定および運賃表IDを一括照合キャッシュ中..." });
                    var validFreightTables = new HashSet<Guid>(await _context.FreightTables.Select(f => f.FreightTableId).ToListAsync());
                    var existingMap = await _context.DistanceFreights.IgnoreQueryFilters()
                        .ToDictionaryAsync(df => (df.FreightTableId, df.DistanceKm, df.Size));

                    // ヘッダー行による動的列位置マッピング
                    int rateIdIdx = -1, distKmIdx = -1, sizeIdx = -1, costIdx = -1, priceIdx = -1;
                    if (rows.Count > 0)
                    {
                        var header = rows[0].Select(h => (h ?? "").Trim().ToLower()).ToList();
                        for (int col = 0; col < header.Count; col++)
                        {
                            var h = header[col];
                            if (h.Contains("運賃表id") || h.Contains("料金表id") || h == "rate_id" || h == "freight_table_id") rateIdIdx = col;
                            else if (h.Contains("距離") || h.Contains("km") || h == "distance_km") distKmIdx = col;
                            else if (h.Contains("大きさ") || h.Contains("サイズ") || h == "size") sizeIdx = col;
                            else if (h.Contains("原価") || h == "cost") costIdx = col;
                            else if (h.Contains("売価") || h.Contains("買価") || h == "price") priceIdx = col;
                        }
                    }

                    // フェーズ4: データインポート・書き込み開始
                    await sendProgressAsync(new { status = "start", title = "【4/4】DB一括更新中...", current = 0, total = total, currentKey = "" });

                    int processedCount = 0;
                    int batchSize = 1000;

                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 3) continue;

                        string rateIdStr = "", distKmStr = "", sizeStr = "", costStr = "", priceStr = "";

                        if (rateIdIdx != -1 && rateIdIdx < r.Length) rateIdStr = (r[rateIdIdx] ?? "").Trim();
                        if (distKmIdx != -1 && distKmIdx < r.Length) distKmStr = (r[distKmIdx] ?? "").Trim();
                        if (sizeIdx != -1 && sizeIdx < r.Length) sizeStr = (r[sizeIdx] ?? "").Trim();
                        if (costIdx != -1 && costIdx < r.Length) costStr = (r[costIdx] ?? "").Trim();
                        if (priceIdx != -1 && priceIdx < r.Length) priceStr = (r[priceIdx] ?? "").Trim();

                        // ヘッダー非適合・フォールバック判定
                        if (string.IsNullOrWhiteSpace(sizeStr) || string.IsNullOrWhiteSpace(costStr))
                        {
                            if (r.Length >= 6 && Guid.TryParse((r[0] ?? "").Trim(), out _) && Guid.TryParse((r[1] ?? "").Trim(), out _))
                            {
                                rateIdStr = (r[1] ?? "").Trim();
                                distKmStr = (r[2] ?? "").Trim();
                                sizeStr = (r[3] ?? "").Trim();
                                costStr = (r[4] ?? "").Trim();
                                priceStr = (r[5] ?? "").Trim();
                            }
                            else if (r.Length >= 5 && Guid.TryParse((r[0] ?? "").Trim(), out _))
                            {
                                rateIdStr = (r[0] ?? "").Trim();
                                distKmStr = (r[1] ?? "").Trim();
                                sizeStr = (r[2] ?? "").Trim();
                                costStr = (r[3] ?? "").Trim();
                                priceStr = (r[4] ?? "").Trim();
                            }
                            else
                            {
                                rateIdStr = defaultFreightTableId?.ToString() ?? "";
                                distKmStr = (r[0] ?? "").Trim();
                                sizeStr = (r[1] ?? "").Trim();
                                costStr = (r[2] ?? "").Trim();
                                priceStr = (r[3] ?? "").Trim();
                            }
                        }

                        string rowDetail = string.Join(", ", r.Select((v, idx) => $"[{idx + 1}列目='{v}']"));

                        if (string.IsNullOrWhiteSpace(rateIdStr) && defaultFreightTableId.HasValue && defaultFreightTableId.Value != Guid.Empty)
                        {
                            rateIdStr = defaultFreightTableId.Value.ToString();
                        }

                        if (!Guid.TryParse(rateIdStr, out var rateId))
                        {
                            throw new Exception($"{i + 1}行目: 料金表を選択するか、CSV内に有効な料金表IDを指定してください。入力された値: '{rateIdStr}' (取り込んだ行データ: {rowDetail})");
                        }
                        if (!int.TryParse(distKmStr, out var distanceKm))
                        {
                            throw new Exception($"{i + 1}行目: 距離(km)には数値を指定してください。入力された値: '{distKmStr}' (取り込んだ行データ: {rowDetail})");
                        }
                        if (!int.TryParse(sizeStr, out var size))
                        {
                            throw new Exception($"{i + 1}行目: サイズには数値を指定してください。入力された値: '{sizeStr}' (取り込んだ行データ: {rowDetail})");
                        }
                        if (!int.TryParse(costStr, out var cost))
                        {
                            throw new Exception($"{i + 1}行目: 原価には数値を指定してください。入力された値: '{costStr}' (取り込んだ行データ: {rowDetail})");
                        }
                        if (!int.TryParse(priceStr, out var price))
                        {
                            throw new Exception($"{i + 1}行目: 売価には数値を指定してください。入力された値: '{priceStr}' (取り込んだ行データ: {rowDetail})");
                        }

                        if (!validFreightTables.Contains(rateId))
                        {
                            throw new Exception($"{i + 1}行目: 指定された料金表ID ({rateIdStr}) がマスターに存在しません。(取り込んだ行データ: {rowDetail})");
                        }

                        var key = (rateId, distanceKm, size);
                        if (!existingMap.TryGetValue(key, out var existing))
                        {
                            if (!createIfNotFound)
                            {
                                throw new Exception($"{i + 1}行目: 指定された運賃設定 (運賃表ID: {rateIdStr}, 距離: {distanceKm}km, サイズ: {size}) は存在しません。");
                            }
                            var newFreight = new DistanceFreight
                            {
                                FreightId = Guid.NewGuid(),
                                FreightTableId = rateId,
                                DistanceKm = distanceKm,
                                Size = size,
                                Cost = cost,
                                Price = price
                            };
                            _context.DistanceFreights.Add(newFreight);
                            existingMap[key] = newFreight;
                        }
                        else
                        {
                            existing.Cost = cost;
                            existing.Price = price;
                            existing.IsDeleted = false;
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
                                currentKey = $"距離: {distanceKm}km, サイズ: {size}" 
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
                        message = $"輸送運賃マスターのインポートが完了しました。（全 {processedCount:N0} 件 / 処理時間: {totalSec:F1}秒）" 
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
