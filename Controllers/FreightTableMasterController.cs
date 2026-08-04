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
    /// 運賃表マスター（m_freight_table）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class FreightTableMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public FreightTableMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 運賃表マスター一覧画面を表示します。
        /// </summary>
        /// <param name="searchName">運賃表名検索キーワード</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(string? searchName, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_FreightTable";
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

            var query = _context.FreightTables.Include(f => f.Carrier).IgnoreQueryFilters().AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(f => !f.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(f => f.RateName.Contains(searchName));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(f => f.RateName)
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
        /// 運賃表マスターを登録または更新します。
        /// </summary>
        /// <param name="freightTable">入力運賃表モデル</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(FreightTable freightTable)
        {
            if (freightTable.FreightTableId == Guid.Empty)
            {
                freightTable.FreightTableId = Guid.NewGuid();
                _context.FreightTables.Add(freightTable);
            }
            else
            {
                var existing = await _context.FreightTables.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.FreightTableId == freightTable.FreightTableId);
                if (existing != null)
                {
                    existing.RateName = freightTable.RateName;
                    existing.CarrierId = freightTable.CarrierId;
                    existing.RateTableType = freightTable.RateTableType;
                    existing.IsDeleted = false;
                    _context.FreightTables.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 運賃表を論理削除します。
        /// </summary>
        /// <param name="id">運賃表ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.FreightTables.FindAsync(id);
            if (item != null)
            {
                _context.FreightTables.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された運賃表を復元します。
        /// </summary>
        /// <param name="id">運賃表ID</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var item = await _context.FreightTables.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.FreightTableId == id);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の運賃表を一括削除します。
        /// </summary>
        /// <param name="ids">対象運賃表ID配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(Guid[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var items = await _context.FreightTables.Where(f => ids.Contains(f.FreightTableId)).ToListAsync();
                _context.FreightTables.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全運賃表マスターをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.FreightTables.ToListAsync();
            var headers = new[] { "運賃表ID", "料金表名", "料金表種別(1:個配/2:路線/3:チャーター)", "運送会社ID" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, f => new[]
            {
                f.FreightTableId.ToString(),
                f.RateName,
                f.RateTableType.ToString(),
                f.CarrierId.ToString()
            });

            return File(bytes, "text/csv; charset=utf-8", "m_freight_table.csv");
        }

        /// <summary>
        /// CSVファイルから運賃表マスターを一括インポート（追加・更新）します。
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
                        if (r.Length < 4) continue;

                        string idStr = r[0];
                        string name = r[1];
                        string typeStr = r[2];
                        string carrierIdStr = r[3];

                        if (!int.TryParse(typeStr, out var rateTableType))
                        {
                            rateTableType = 1;
                        }
                        if (!Guid.TryParse(carrierIdStr, out var carrierId))
                        {
                            throw new Exception($"{i + 1}行目: 運送会社IDのフォーマットが不正です。({carrierIdStr})");
                        }

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newEntity = new FreightTable
                            {
                                FreightTableId = Guid.NewGuid(),
                                RateName = name,
                                RateTableType = rateTableType,
                                CarrierId = carrierId
                            };
                            _context.FreightTables.Add(newEntity);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 運賃表IDのフォーマットが不正です。({idStr})");
                            }
                            var existing = await _context.FreightTables.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.FreightTableId == id)
                                        ?? _context.FreightTables.Local.FirstOrDefault(f => f.FreightTableId == id);

                            if (existing == null)
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された運賃表ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new FreightTable
                                {
                                    FreightTableId = id,
                                    RateName = name,
                                    RateTableType = rateTableType,
                                    CarrierId = carrierId
                                };
                                _context.FreightTables.Add(existing);
                            }
                            else
                            {
                                existing.RateName = name;
                                existing.RateTableType = rateTableType;
                                existing.CarrierId = carrierId;
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
        /// SSE ストリーミングを用いて運賃表マスターをリアルタイム進捗表示付きで高パフォーマンスに一括インポートします。
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task ImportCsvStream(IFormFile csvFile, bool createIfNotFound = false, Guid? defaultCarrierId = null)
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
                if (defaultCarrierId.HasValue && defaultCarrierId.Value != Guid.Empty)
                {
                    bool carrierExists = await _context.Carriers.AnyAsync(c => c.CarrierId == defaultCarrierId.Value && !c.IsDeleted);
                    if (!carrierExists)
                    {
                        await sendProgressAsync(new { status = "error", message = "指定されたデフォルト運送会社がマスターに存在しません。" });
                        return;
                    }
                }
                else
                {
                    bool hasEmptyCarrierKey = false;
                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 2) continue;
                        string carrierIdStr = r.Length > 3 ? (r[3] ?? "").Trim() : "";
                        if (string.IsNullOrWhiteSpace(carrierIdStr))
                        {
                            hasEmptyCarrierKey = true;
                            break;
                        }
                    }

                    if (hasEmptyCarrierKey)
                    {
                        await sendProgressAsync(new { status = "need_selection", missing = new[] { "carrier" }, message = "運送会社IDが未指定のデータが検出されました。適用する運送会社を選択してください。" });
                        return;
                    }
                }

                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    var existingMap = await _context.FreightTables.IgnoreQueryFilters().ToDictionaryAsync(ft => ft.FreightTableId);
                    var validCarriers = new HashSet<Guid>(await _context.Carriers.Select(c => c.CarrierId).ToListAsync());

                    int total = rows.Count - 1;
                    await sendProgressAsync(new { status = "start", current = 0, total = total, currentKey = "" });

                    int processedCount = 0;
                    int batchSize = 1000;

                    for (int i = 1; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r.Length < 2) continue;

                        string idStr, name, typeStr, carrierIdStr;

                        // 4列形式（運賃表ID, 料金表名, 料金表種別, 運送会社ID）と 3列/2列形式の自動判定
                        if (r.Length >= 4 && Guid.TryParse((r[0] ?? "").Trim(), out _))
                        {
                            idStr = (r[0] ?? "").Trim();
                            name = (r[1] ?? "").Trim();
                            typeStr = (r[2] ?? "").Trim();
                            carrierIdStr = (r[3] ?? "").Trim();
                        }
                        else if (r.Length >= 3)
                        {
                            idStr = (r[0] ?? "").Trim();
                            name = (r[1] ?? "").Trim();
                            typeStr = (r[2] ?? "").Trim();
                            carrierIdStr = r.Length > 3 ? (r[3] ?? "").Trim() : "";
                        }
                        else
                        {
                            idStr = "";
                            name = (r[0] ?? "").Trim();
                            typeStr = (r[1] ?? "").Trim();
                            carrierIdStr = "";
                        }

                        if (string.IsNullOrWhiteSpace(name)) continue;

                        if (string.IsNullOrWhiteSpace(carrierIdStr) && defaultCarrierId.HasValue && defaultCarrierId.Value != Guid.Empty)
                        {
                            carrierIdStr = defaultCarrierId.Value.ToString();
                        }

                        if (!int.TryParse(typeStr, out var rateType))
                        {
                            throw new Exception($"{i + 1}行目: 運賃表種別は数値(1:個配, 2:路線, 3:チャーター)を指定してください。");
                        }

                        Guid carrierId = Guid.Empty;
                        if (!string.IsNullOrWhiteSpace(carrierIdStr) && Guid.TryParse(carrierIdStr, out var cId))
                        {
                            if (!validCarriers.Contains(cId))
                            {
                                throw new Exception($"{i + 1}行目: 指定された運送会社ID ({carrierIdStr}) が存在しません。");
                            }
                            carrierId = cId;
                        }
                        else if (defaultCarrierId.HasValue && defaultCarrierId.Value != Guid.Empty)
                        {
                            carrierId = defaultCarrierId.Value;
                        }
                        else
                        {
                            throw new Exception($"{i + 1}行目: 運送会社を選択するか、CSV内に運送会社IDを指定してください。");
                        }

                        if (string.IsNullOrWhiteSpace(idStr))
                        {
                            var newFt = new FreightTable
                            {
                                FreightTableId = Guid.NewGuid(),
                                RateName = name,
                                RateTableType = rateType,
                                CarrierId = carrierId
                            };
                            _context.FreightTables.Add(newFt);
                        }
                        else
                        {
                            if (!Guid.TryParse(idStr, out var id))
                            {
                                throw new Exception($"{i + 1}行目: 運賃表IDのフォーマットが不正です。({idStr})");
                            }

                            if (!existingMap.TryGetValue(id, out var existing))
                            {
                                if (!createIfNotFound)
                                {
                                    throw new Exception($"{i + 1}行目: 指定された運賃表ID ({idStr}) のレコードが存在しません。");
                                }
                                existing = new FreightTable
                                {
                                    FreightTableId = id,
                                    RateName = name,
                                    RateTableType = rateType,
                                    CarrierId = carrierId
                                };
                                _context.FreightTables.Add(existing);
                                existingMap[id] = existing;
                            }
                            else
                            {
                                existing.RateName = name;
                                existing.RateTableType = rateType;
                                if (carrierId != Guid.Empty) existing.CarrierId = carrierId;
                                existing.IsDeleted = false;
                            }
                        }

                        processedCount++;

                        if (processedCount % 100 == 0 || i == rows.Count - 1)
                        {
                            await sendProgressAsync(new { status = "processing", current = processedCount, total = total, currentKey = name });
                        }

                        if (processedCount % batchSize == 0)
                        {
                            await _context.SaveChangesAsync();
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await sendProgressAsync(new { status = "completed", current = processedCount, total = total, message = $"運賃表マスターのインポートが完了しました。（全 {processedCount:N0} 件）" });
                });
            }
            catch (Exception ex)
            {
                await sendProgressAsync(new { status = "error", message = $"インポートエラー: {RouteXWms.Helpers.ErrorHelper.ToUserFriendlyMessage(ex)}" });
            }
        }
    }
}
