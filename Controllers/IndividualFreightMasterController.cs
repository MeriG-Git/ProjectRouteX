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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var rows = await CsvService.ReadCsvAsync(csvFile);
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
                TempData["SuccessMessage"] = "CSVデータの取込が完了しました。";
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
