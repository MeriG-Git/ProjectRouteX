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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var rows = await CsvService.ReadCsvAsync(csvFile);
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
                        throw new Exception($"{i + 1}行目: 運送会社IDのフォーマットが不正です。");
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
                            throw new Exception($"{i + 1}行目: 運賃表IDのフォーマットが不正です。");
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
