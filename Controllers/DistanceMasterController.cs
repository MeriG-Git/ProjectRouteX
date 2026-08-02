using System;
using System.Collections.Generic;
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
    /// 距離マスター（m_distance）のCRUD操作・検索・CSV入出力を管理するコントローラー
    /// </summary>
    public class DistanceMasterController : Controller
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public DistanceMasterController(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 距離マスター一覧画面を表示します。
        /// </summary>
        /// <param name="freightTableId">運賃表ID絞り込み条件</param>
        /// <param name="searchCity">市区町村コード検索文字列</param>
        /// <param name="showDeleted">削除済み表示フラグ</param>
        /// <param name="page">ページ番号</param>
        /// <param name="pageSize">1ページあたりの表示件数</param>
        /// <returns>一覧画面ビュー</returns>
        public async Task<IActionResult> Index(Guid? freightTableId, string? searchCity, bool showDeleted = false, int page = 1, int? pageSize = null)
        {
            const string cookieKey = "PageSize_Master_Distance";
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

            var query = _context.Distances
                .Include(d => d.FreightTable)
                .IgnoreQueryFilters()
                .AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(d => !d.IsDeleted);
            }

            if (freightTableId.HasValue && freightTableId.Value != Guid.Empty)
            {
                query = query.Where(d => d.FreightTableId == freightTableId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchCity))
            {
                query = query.Where(d => d.CityCode.Contains(searchCity));
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(d => d.CityCode)
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.FreightTables = await _context.FreightTables
                .OrderBy(t => t.RateName)
                .ToListAsync();

            ViewBag.SelectedRateId = freightTableId;
            ViewBag.SearchCity = searchCity;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.Page = page;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize);

            return View(items);
        }

        /// <summary>
        /// 距離マスターを登録または更新します。
        /// </summary>
        /// <param name="freightTableId">運賃表ID</param>
        /// <param name="cityCode">市区町村コード</param>
        /// <param name="originalFreightTableId">変更前の運賃表ID</param>
        /// <param name="originalCityCode">変更前の市区町村コード</param>
        /// <param name="distanceKm">距離（km）</param>
        /// <param name="isNew">新規追加フラグ</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Guid freightTableId, string cityCode, Guid originalFreightTableId, string originalCityCode, int distanceKm, bool isNew)
        {
            cityCode = (cityCode ?? "").Trim();
            if (cityCode.Length > 5) cityCode = cityCode.Substring(0, 5);

            if (isNew)
            {
                var existing = await _context.Distances.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(d => d.FreightTableId == freightTableId && d.CityCode == cityCode);

                if (existing != null)
                {
                    existing.DistanceKm = distanceKm;
                    existing.IsDeleted = false;
                    _context.Distances.Update(existing);
                }
                else
                {
                    var newDist = new Distance
                    {
                        FreightTableId = freightTableId,
                        CityCode = cityCode,
                        DistanceKm = distanceKm
                    };
                    _context.Distances.Add(newDist);
                }
            }
            else
            {
                var original = await _context.Distances.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(d => d.FreightTableId == originalFreightTableId && d.CityCode == originalCityCode);

                if (original != null)
                {
                    _context.Distances.Remove(original);
                }

                var existingNew = await _context.Distances.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(d => d.FreightTableId == freightTableId && d.CityCode == cityCode);

                if (existingNew != null)
                {
                    existingNew.DistanceKm = distanceKm;
                    existingNew.IsDeleted = false;
                    _context.Distances.Update(existingNew);
                }
                else
                {
                    var newDist = new Distance
                    {
                        FreightTableId = freightTableId,
                        CityCode = cityCode,
                        DistanceKm = distanceKm
                    };
                    _context.Distances.Add(newDist);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 指定された距離マスターを論理削除します。
        /// </summary>
        /// <param name="freightTableId">運賃表ID</param>
        /// <param name="cityCode">市区町村コード</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid freightTableId, string cityCode)
        {
            var item = await _context.Distances.IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.FreightTableId == freightTableId && d.CityCode == cityCode);
            if (item != null)
            {
                _context.Distances.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 論理削除された距離マスターを復元します。
        /// </summary>
        /// <param name="freightTableId">運賃表ID</param>
        /// <param name="cityCode">市区町村コード</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid freightTableId, string cityCode)
        {
            var item = await _context.Distances.IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.FreightTableId == freightTableId && d.CityCode == cityCode);
            if (item != null)
            {
                item.IsDeleted = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 選択された複数の距離マスターを一括削除します。
        /// </summary>
        /// <param name="compositeIds">複合キー（運賃表ID_市区町村コード）の配列</param>
        /// <returns>一覧画面へのリダイレクト</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(string[] compositeIds)
        {
            if (compositeIds != null && compositeIds.Length > 0)
            {
                foreach (var cid in compositeIds)
                {
                    var parts = cid.Split('_');
                    if (parts.Length == 2 && Guid.TryParse(parts[0], out var freightTableId))
                    {
                        string cityCode = parts[1];
                        var item = await _context.Distances.IgnoreQueryFilters()
                            .FirstOrDefaultAsync(d => d.FreightTableId == freightTableId && d.CityCode == cityCode);
                        if (item != null)
                        {
                            _context.Distances.Remove(item);
                        }
                    }
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 全距離マスターをBOM付きUTF-8形式のCSVファイルとして出力します。
        /// </summary>
        /// <returns>CSVレスポンス</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var items = await _context.Distances
                .Include(d => d.FreightTable)
                .ToListAsync();

            var headers = new[] { "運賃表ID", "市区町村コード", "距離キロ" };
            var bytes = CsvService.ExportToCsvBytes(items, headers, d => new[]
            {
                d.FreightTableId.ToString(),
                d.CityCode,
                d.DistanceKm.ToString()
            });

            return File(bytes, "text/csv; charset=utf-8", "m_distance.csv");
        }

        /// <summary>
        /// CSVファイルから距離マスターを一括インポート（追加・更新）します。
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
                    if (r.Length < 3) continue;

                    string rateIdStr = r[0];
                    string cityCode = (r[1] ?? "").Trim();
                    string distKmStr = r[2];

                    if (!Guid.TryParse(rateIdStr, out var rateId))
                    {
                        throw new Exception($"{i + 1}行目: 料金表IDのフォーマットが不正です。");
                    }
                    if (string.IsNullOrWhiteSpace(cityCode))
                    {
                        throw new Exception($"{i + 1}行目: 市区町村コードが空です。");
                    }
                    if (!int.TryParse(distKmStr, out var distanceKm))
                    {
                        throw new Exception($"{i + 1}行目: 距離キロのフォーマットが不正です。");
                    }

                    var tableExists = await _context.FreightTables.AnyAsync(t => t.FreightTableId == rateId);
                    if (!tableExists)
                    {
                        throw new Exception($"{i + 1}行目: 指定された運賃表ID ({rateIdStr}) が登録されていません。");
                    }

                    var existing = await _context.Distances.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(d => d.FreightTableId == rateId && d.CityCode == cityCode);

                    if (existing == null)
                    {
                        if (!createIfNotFound)
                        {
                            throw new Exception($"{i + 1}行目: 指定されたマッピング (料金表ID: {rateIdStr}, 市区町村コード: {cityCode}) は存在しません。");
                        }
                        var newDist = new Distance
                        {
                            FreightTableId = rateId,
                            CityCode = cityCode,
                            DistanceKm = distanceKm
                        };
                        _context.Distances.Add(newDist);
                    }
                    else
                    {
                        existing.DistanceKm = distanceKm;
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
