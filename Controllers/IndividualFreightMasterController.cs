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
    /// 個別運賃マスター（m_individual_freight）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// 都道府県別の個配運賃金額の設定を行います。
    /// </summary>
    public class IndividualFreightMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public IndividualFreightMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 個別運賃マスター一覧画面を表示します。
        /// </summary>
        /// <param name="freightTableId">運賃表ID絞り込み条件</param>
        /// <param name="searchName">都道府県・運賃表名検索キーワード</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(Guid? freightTableId, string? searchName, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_IndividualFreight";
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

            var query = _context.IndividualFreights.Include(i => i.FreightTable).ThenInclude(f => f!.Carrier).IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(i => !i.IsDeleted);
            }

            if (freightTableId.HasValue && freightTableId.Value != Guid.Empty)
            {
                query = query.Where(i => i.FreightTableId == freightTableId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(i => (i.FreightTable != null && i.FreightTable.RateName.Contains(searchName)) || i.PrefCode.Contains(searchName) || i.PrefName.Contains(searchName));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(i => i.FreightTable != null ? i.FreightTable.RateName : "").ThenBy(i => i.PrefCode)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.FreightTables = await _context.FreightTables.Include(f => f.Carrier).OrderBy(f => f.RateName).ToListAsync();
            ViewBag.SelectedFreightTableId = freightTableId;
            ViewBag.SearchName = searchName;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 個別運賃マスターを登録または更新します。
        /// </summary>
        /// <param name="individualFreight">入力モデル</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(IndividualFreight individualFreight)
        {
            if (individualFreight.IndividualFreightId == Guid.Empty)
            {
                individualFreight.IndividualFreightId = Guid.NewGuid();
                _context.IndividualFreights.Add(individualFreight);
            }
            else
            {
                var existing = await _context.IndividualFreights.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.IndividualFreightId == individualFreight.IndividualFreightId);
                if (existing != null)
                {
                    existing.FreightTableId = individualFreight.FreightTableId;
                    existing.PrefCode = individualFreight.PrefCode;
                    existing.PrefName = individualFreight.PrefName;
                    existing.Size = individualFreight.Size;
                    existing.Weight = individualFreight.Weight;
                    existing.Cost = individualFreight.Cost;
                    existing.Price = individualFreight.Price;
                    existing.IsDeleted = false;
                    _context.IndividualFreights.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 個別運賃を論理削除します。
        /// </summary>
        /// <param name="id">個別運賃ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.IndividualFreights.FindAsync(id);
            if (item != null)
            {
                _context.IndividualFreights.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された個別運賃を復元します。
        /// </summary>
        /// <param name="id">個別運賃ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var item = await _context.IndividualFreights.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.IndividualFreightId == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の個別運賃を一括削除します。
        /// </summary>
        /// <param name="ids">対象個別運賃ID配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(Guid[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.IndividualFreights.Where(i => ids.Contains(i.IndividualFreightId)).ToListAsync();
                _context.IndividualFreights.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全個別運賃マスターをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.IndividualFreights.Include(i => i.FreightTable).ToListAsync();
            var headers = new[] { "個配運賃ID", "運賃表ID", "県コード", "都道府県名", "大きさ", "重量", "原価", "売価" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, i => new[]
            {
                i.IndividualFreightId.ToString(),
                i.FreightTableId.ToString(),
                i.PrefCode,
                i.PrefName,
                i.Size.ToString(),
                i.Weight.ToString(),
                i.Cost.ToString(),
                i.Price.ToString()
            });

            return File(bytes, "text/csv; charset=utf-8", "m_individual_freight.csv");
        }

        /// <summary>
        /// CSVファイルから個別運賃マスターを一括インポート（追加・更新）します。
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
                        string freightTableIdStr = r[1];
                        string prefCode = r[2];
                        string prefName = r[3];
                        string sizeStr = r.Length > 4 ? r[4] : "0";
                        string weightStr = r.Length > 5 ? r[5] : "0";
                        string costStr = r.Length > 6 ? r[6] : "0";
                        string priceStr = r.Length > 7 ? r[7] : (r.Length > 4 ? r[4] : "0");

                        if (!Guid.TryParse(freightTableIdStr, out var freightTableId))
                        {
                            throw new Exception($"{i + 1}行目: 運賃表IDのフォーマットが不正です。");
                        }

                        var tableExists = await _context.FreightTables.AnyAsync(f => f.FreightTableId == freightTableId);
                        if (!tableExists)
                        {
                            throw new Exception($"{i + 1}行目: 指定された運賃表ID ({freightTableIdStr}) が登録されていません。");
                        }

                        if (string.IsNullOrWhiteSpace(prefCode))
                        {
                            throw new Exception($"{i + 1}行目: 県コードは必須です。");
                        }
                        if (string.IsNullOrWhiteSpace(prefName))
                        {
                            throw new Exception($"{i + 1}行目: 都道府県名は必須です。");
                        }
                        int.TryParse(sizeStr, out var size);
                        int.TryParse(weightStr, out var weight);
                        int.TryParse(costStr, out var cost);
                        if (!int.TryParse(priceStr, out var price))
                        {
                            throw new Exception($"{i + 1}行目: 売価（金額）の数値フォーマットが不正です。");
                        }

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newEntity = new IndividualFreight
                            {
                                IndividualFreightId = Guid.NewGuid(),
                                FreightTableId = freightTableId,
                                PrefCode = prefCode,
                                PrefName = prefName,
                                Size = size,
                                Weight = weight,
                                Cost = cost,
                                Price = price
                            };
                            _context.IndividualFreights.Add(newEntity);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 個配運賃IDのフォーマットが不正です。");
                            }
                            var existing = await _context.IndividualFreights.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.IndividualFreightId == id)
                                        ?? _context.IndividualFreights.Local.FirstOrDefault(x => x.IndividualFreightId == id);

                            if (existing == null)
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された個配運賃ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new IndividualFreight
                                {
                                    IndividualFreightId = id,
                                    FreightTableId = freightTableId,
                                    PrefCode = prefCode,
                                    PrefName = prefName,
                                    Size = size,
                                    Weight = weight,
                                    Cost = cost,
                                    Price = price
                                };
                                _context.IndividualFreights.Add(existing);
                            }
                            else
                            {
                                existing.FreightTableId = freightTableId;
                                existing.PrefCode = prefCode;
                                existing.PrefName = prefName;
                                existing.Size = size;
                                existing.Weight = weight;
                                existing.Cost = cost;
                                existing.Price = price;
                                existing.IsDeleted = false;
                                _context.IndividualFreights.Update(existing);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                TempData["SuccessMessage"] = "CSVデータの取込が完了しました。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"インポートエラー: {RouteXWms.Helpers.ErrorHelper.ToUserFriendlyMessage(ex)}";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// SSE ストリーミングを用いて個配運賃マスターをリアルタイム進捗表示付きで高パフォーマンスに一括インポートします。
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

            try
            {
                var rows = await CsvService.ReadCsvAsync(csvFile);

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
                    // 親キー空データの事前検出
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
                    var existingMap = await _context.IndividualFreights.IgnoreQueryFilters()
                        .ToDictionaryAsync(ifr => (ifr.FreightTableId, ifr.PrefCode, ifr.Size));
                    var validFreightTables = new HashSet<Guid>(await _context.FreightTables.Select(f => f.FreightTableId).ToListAsync());

                        // ヘッダー行による動的列位置マッピング
                        int rateIdIdx = -1, prefCodeIdx = -1, prefNameIdx = -1, sizeIdx = -1, costIdx = -1, priceIdx = -1;
                        if (rows.Count > 0)
                        {
                            var header = rows[0].Select(h => (h ?? "").Trim().ToLower()).ToList();
                            for (int col = 0; col < header.Count; col++)
                            {
                                var h = header[col];
                                if (h.Contains("運賃表id") || h.Contains("料金表id") || h == "rate_id" || h == "freight_table_id") rateIdIdx = col;
                                else if (h == "県コード" || h.Contains("都道府県コード") || h == "pref_code") prefCodeIdx = col;
                                else if (h == "都道府県名" || h == "県名" || h == "pref_name") prefNameIdx = col;
                                else if (h.Contains("大きさ") || h.Contains("サイズ") || h == "size") sizeIdx = col;
                                else if (h.Contains("原価") || h == "cost") costIdx = col;
                                else if (h.Contains("売価") || h.Contains("買価") || h == "price") priceIdx = col;
                            }
                        }

                        int total = rows.Count - 1;
                        await sendProgressAsync(new { status = "start", current = 0, total = total, currentKey = "" });

                        int processedCount = 0;
                        int batchSize = 1000;

                        for (int i = 1; i < rows.Count; i++)
                        {
                            var r = rows[i];
                            if (r.Length < 3) continue;

                            string rateIdStr = "", prefCodeStr = "", prefNameStr = "", sizeStr = "", costStr = "", priceStr = "";

                            if (rateIdIdx != -1 && rateIdIdx < r.Length) rateIdStr = (r[rateIdIdx] ?? "").Trim();
                            if (prefCodeIdx != -1 && prefCodeIdx < r.Length) prefCodeStr = (r[prefCodeIdx] ?? "").Trim();
                            if (prefNameIdx != -1 && prefNameIdx < r.Length) prefNameStr = (r[prefNameIdx] ?? "").Trim();
                            if (sizeIdx != -1 && sizeIdx < r.Length) sizeStr = (r[sizeIdx] ?? "").Trim();
                            if (costIdx != -1 && costIdx < r.Length) costStr = (r[costIdx] ?? "").Trim();
                            if (priceIdx != -1 && priceIdx < r.Length) priceStr = (r[priceIdx] ?? "").Trim();

                            // 標準8列構造のフォールバック・確実な列取得
                            if (string.IsNullOrWhiteSpace(sizeStr) || string.IsNullOrWhiteSpace(costStr))
                            {
                                if (r.Length >= 8)
                                {
                                    rateIdStr = (r[1] ?? "").Trim();
                                    prefCodeStr = (r[2] ?? "").Trim();
                                    prefNameStr = (r[3] ?? "").Trim();
                                    sizeStr = (r[4] ?? "").Trim();
                                    costStr = (r[6] ?? "").Trim();
                                    priceStr = (r[7] ?? "").Trim();
                                }
                                else if (r.Length >= 7)
                                {
                                    rateIdStr = (r[0] ?? "").Trim();
                                    prefCodeStr = (r[1] ?? "").Trim();
                                    prefNameStr = (r[2] ?? "").Trim();
                                    sizeStr = (r[3] ?? "").Trim();
                                    costStr = (r[5] ?? "").Trim();
                                    priceStr = (r[6] ?? "").Trim();
                                }
                                else if (r.Length >= 6 && Guid.TryParse((r[0] ?? "").Trim(), out _) && Guid.TryParse((r[1] ?? "").Trim(), out _))
                                {
                                    rateIdStr = (r[1] ?? "").Trim();
                                    prefCodeStr = (r[2] ?? "").Trim();
                                    sizeStr = (r[3] ?? "").Trim();
                                    costStr = (r[4] ?? "").Trim();
                                    priceStr = (r[5] ?? "").Trim();
                                }
                                else if (r.Length >= 5 && Guid.TryParse((r[0] ?? "").Trim(), out _))
                                {
                                    rateIdStr = (r[0] ?? "").Trim();
                                    prefCodeStr = (r[1] ?? "").Trim();
                                    sizeStr = (r[2] ?? "").Trim();
                                    costStr = (r[3] ?? "").Trim();
                                    priceStr = (r[4] ?? "").Trim();
                                }
                                else
                                {
                                    rateIdStr = defaultFreightTableId?.ToString() ?? "";
                                    prefCodeStr = (r[0] ?? "").Trim();
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

                            if (string.IsNullOrWhiteSpace(prefCodeStr) || prefCodeStr.Length > 2)
                            {
                                throw new Exception($"{i + 1}行目: 都道府県コード(県コード)が空か、または桁数(2桁以内)を超えています。入力された値: '{prefCodeStr}' (取り込んだ行データ: {rowDetail})");
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

                            var key = (rateId, prefCodeStr, size);
                            if (!existingMap.TryGetValue(key, out var existing))
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された個配運賃設定 (運賃表ID: {rateIdStr}, 県コード: {prefCodeStr}, サイズ: {size}) は存在しません。");
                                }
                                var newIfr = new IndividualFreight
                                {
                                    IndividualFreightId = Guid.NewGuid(),
                                    FreightTableId = rateId,
                                    PrefCode = prefCodeStr,
                                    PrefName = prefNameStr,
                                    Size = size,
                                    Cost = cost,
                                    Price = price
                                };
                                _context.IndividualFreights.Add(newIfr);
                                existingMap[key] = newIfr;
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(prefNameStr)) existing.PrefName = prefNameStr;
                                existing.Cost = cost;
                                existing.Price = price;
                                existing.IsDeleted = false;
                            }

                        processedCount++;

                        if (processedCount % 100 == 0 || i == rows.Count - 1)
                        {
                            await sendProgressAsync(new { status = "processing", current = processedCount, total = total, currentKey = $"県コード: {prefCodeStr}, サイズ: {size}" });
                        }

                        if (processedCount % batchSize == 0)
                        {
                            await _context.SaveChangesAsync();
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await sendProgressAsync(new { status = "completed", current = processedCount, total = total, message = $"個配運賃マスターのインポートが完了しました。（全 {processedCount:N0} 件）" });
                });
            }
            catch (Exception ex)
            {
                await sendProgressAsync(new { status = "error", message = $"インポートエラー: {RouteXWms.Helpers.ErrorHelper.ToUserFriendlyMessage(ex)}" });
            }
        }
    }
}
