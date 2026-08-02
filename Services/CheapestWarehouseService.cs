using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RouteXWms.Data;
using RouteXWms.Models;

namespace RouteXWms.Services
{
    /// <summary>
    /// 最安倉庫選定結果を保持するデータ構造クラス
    /// </summary>
    public class CheapestWarehouseOptionResult
    {
        /// <summary>選定された最安倉庫ID</summary>
        public Guid? WarehouseId { get; set; }

        /// <summary>適用された運賃表ID</summary>
        public Guid? FreightTableId { get; set; }

        /// <summary>運賃表種別（1: 個配, 2: 路線, 3: チャーター等）</summary>
        public int RateTableType { get; set; } = 1;

        /// <summary>計算された最安運賃合計売価（円）</summary>
        public decimal? CalculatedPrice { get; set; }

        /// <summary>在庫充足フラグ（指定数量を満たす在庫が存在するか）</summary>
        public bool HasStock { get; set; }

        /// <summary>運賃算出成功フラグ（条件に該当する運賃マスターが存在するか）</summary>
        public bool IsPriceFound { get; set; }
    }

    /// <summary>
    /// 最安倉庫自動選定および在庫引当・解除アルゴリズムを提供するサービス
    /// </summary>
    public class CheapestWarehouseService
    {
        private readonly WmsDbContext _context;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public CheapestWarehouseService(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 指定された出荷条件（荷主、商品、運送会社、お届け先郵便番号、出荷ピース数）から、
        /// 在庫を保有し、かつ運賃が最も安くなる倉庫および運賃表を選定します。
        /// </summary>
        /// <param name="shipperId">荷主ID</param>
        /// <param name="productId">商品コード</param>
        /// <param name="carrierId">指定運送会社ID</param>
        /// <param name="zipCode">お届け先郵便番号</param>
        /// <param name="totalPieces">出荷総ピース数</param>
        /// <param name="is30KgFixed">重量30kg固定計算フラグ</param>
        /// <returns>最安倉庫選定結果オブジェクト</returns>
        public async Task<CheapestWarehouseOptionResult> FindCheapestWarehouseOptionAsync(
            Guid shipperId,
            string productId,
            Guid carrierId,
            string zipCode,
            int totalPieces,
            bool is30KgFixed)
        {
            var result = new CheapestWarehouseOptionResult();

            // 1. 商品情報取得と箱数・余りバラ数の算出
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == productId);
            int unitQuantity = product?.Quantity > 0 ? product.Quantity : 1;

            int fullCases = totalPieces / unitQuantity;
            int remainderPieces = totalPieces % unitQuantity;
            int totalBoxes = fullCases + (remainderPieces > 0 ? 1 : 0);

            // 2. 指定運送会社の運賃表に紐づく倉庫一覧を取得
            var rateMappings = await _context.WarehouseDistanceRates
                .Include(w => w.FreightTable)
                .Where(w => w.FreightTable!.CarrierId == carrierId && !w.IsDeleted)
                .ToListAsync();

            var mappedWarehouseIds = rateMappings.Select(w => w.WarehouseId).Distinct().ToList();

            // 3. 要求数量（totalPieces）以上の有効在庫を保有する候補倉庫を抽出
            var warehouseStockSums = await _context.Inventories
                .Where(i => i.ShipperId == shipperId && i.ProductId == productId && i.Status == 1 && !i.IsDeleted && mappedWarehouseIds.Contains(i.WarehouseId))
                .GroupBy(i => i.WarehouseId)
                .Select(g => new { WarehouseId = g.Key, AvailablePieces = g.Sum(x => x.CurrentQuantity) })
                .Where(g => g.AvailablePieces >= totalPieces)
                .ToListAsync();

            // 在庫がどの倉庫にも不足している場合
            if (!warehouseStockSums.Any())
            {
                result.HasStock = false;
                result.IsPriceFound = false;
                return result;
            }

            result.HasStock = true;
            var candidateWhIds = warehouseStockSums.Select(w => w.WarehouseId).ToList();

            // 4. 郵便番号マスターから市区町村コード・都道府県コードの補正・取得
            var cleanZip = zipCode?.Replace("-", "").Trim() ?? "";
            var zipEntry = await _context.ZipCodes.FirstOrDefaultAsync(z => z.ZipCodeValue == cleanZip);
            var cityCode = zipEntry?.CityCode;
            var prefCode = zipEntry?.PrefCode;

            // 5. 距離運賃計算用の出荷重量算出（ケース当たり重量 × 梱包箱数）
            int unitWeight = is30KgFixed ? 30 : (product?.Weight ?? 30);
            int totalWeight = unitWeight * totalBoxes;

            // 6. 各候補倉庫 × 運賃表ごとの運賃比較（最小コスト探索）
            int minCost = int.MaxValue;
            Guid? cheapestWhId = null;
            Guid? cheapestFreightTableId = null;
            int cheapestRateTableType = 1;

            foreach (var whId in candidateWhIds)
            {
                var whMappings = rateMappings.Where(w => w.WarehouseId == whId).ToList();

                foreach (var mapping in whMappings)
                {
                    var ft = mapping.FreightTable;
                    if (ft == null) continue;

                    if (ft.RateTableType == 1) // 個配運賃（宅配便タイプ）
                    {
                        if (!string.IsNullOrEmpty(prefCode))
                        {
                            string targetPref = prefCode.Trim();
                            string targetPrefNoZero = targetPref.TrimStart('0');

                            // ステップA: 指定の FreightTableId で都道府県運賃を検索
                            var indFreightList = await _context.IndividualFreights
                                .Where(i => i.FreightTableId == ft.FreightTableId && !i.IsDeleted &&
                                           (i.PrefCode.Trim() == targetPref || i.PrefCode.Trim() == targetPrefNoZero))
                                .ToListAsync();

                            // ステップB: なければ同運送会社の他個配運賃表からフォールバック検索
                            if (!indFreightList.Any())
                            {
                                var carrierFtIds = await _context.FreightTables
                                    .Where(f => f.CarrierId == ft.CarrierId && f.RateTableType == 1 && !f.IsDeleted)
                                    .Select(f => f.FreightTableId)
                                    .ToListAsync();

                                indFreightList = await _context.IndividualFreights
                                    .Where(i => carrierFtIds.Contains(i.FreightTableId) && !i.IsDeleted &&
                                               (i.PrefCode.Trim() == targetPref || i.PrefCode.Trim() == targetPrefNoZero))
                                    .ToListAsync();
                            }

                            // ステップC: なければ全個配運賃マスターから最終フォールバック検索
                            if (!indFreightList.Any())
                            {
                                indFreightList = await _context.IndividualFreights
                                    .Where(i => !i.IsDeleted && (i.PrefCode.Trim() == targetPref || i.PrefCode.Trim() == targetPrefNoZero))
                                    .ToListAsync();
                            }

                            IndividualFreight? indFreight = null;
                            if (indFreightList.Any())
                            {
                                // 重量・サイズ区分が合致するものを優先選定
                                indFreight = indFreightList.Where(i => (i.Weight > 0 && i.Weight >= unitWeight) || (i.Size > 0 && i.Size >= unitWeight))
                                                           .OrderBy(i => i.Weight > 0 ? i.Weight : (i.Size > 0 ? i.Size : 999999))
                                                           .FirstOrDefault()
                                             ?? indFreightList.OrderBy(i => i.Price).FirstOrDefault();
                            }

                            if (indFreight != null)
                            {
                                int calculatedPrice = indFreight.Price * totalBoxes;
                                if (calculatedPrice < minCost)
                                {
                                    minCost = calculatedPrice;
                                    cheapestWhId = whId;
                                    cheapestFreightTableId = ft.FreightTableId;
                                    cheapestRateTableType = ft.RateTableType;
                                }
                            }
                        }
                    }
                    else // 路線運賃など（距離ベース計算）
                    {
                        if (!string.IsNullOrEmpty(cityCode))
                        {
                            // 距離マスターからお届け先市区町村までの距離（km）を取得
                            var distance = await _context.Distances
                                .FirstOrDefaultAsync(d => d.CityCode == cityCode && d.FreightTableId == ft.FreightTableId && !d.IsDeleted);

                            if (distance != null)
                            {
                                var availableSizes = await _context.DistanceFreights
                                    .Where(f => f.FreightTableId == ft.FreightTableId && !f.IsDeleted)
                                    .Select(f => f.Size)
                                    .Distinct()
                                    .ToListAsync();

                                int targetSize = totalWeight;
                                if (availableSizes.Any() && !availableSizes.Contains(targetSize))
                                {
                                    var ceilingSize = availableSizes.Where(s => s >= targetSize).OrderBy(s => s).Cast<int?>().FirstOrDefault();
                                    targetSize = ceilingSize ?? availableSizes.Max();
                                }

                                // 該当サイズ・該当距離帯の最安運賃を選択
                                var freight = await _context.DistanceFreights
                                    .Where(f => f.FreightTableId == ft.FreightTableId
                                             && f.Size == targetSize
                                             && f.DistanceKm >= distance.DistanceKm
                                             && !f.IsDeleted)
                                    .OrderBy(f => f.DistanceKm)
                                    .FirstOrDefaultAsync()
                                    ?? await _context.DistanceFreights
                                    .Where(f => f.FreightTableId == ft.FreightTableId
                                             && f.Size == targetSize
                                             && !f.IsDeleted)
                                    .OrderByDescending(f => f.DistanceKm)
                                    .FirstOrDefaultAsync();

                                if (freight != null && freight.Price < minCost)
                                {
                                    minCost = freight.Price;
                                    cheapestWhId = whId;
                                    cheapestFreightTableId = ft.FreightTableId;
                                    cheapestRateTableType = ft.RateTableType;
                                }
                            }
                        }
                    }
                }
            }

            // 7. 選定結果のまとめ
            if (minCost != int.MaxValue && cheapestWhId.HasValue)
            {
                result.WarehouseId = cheapestWhId;
                result.FreightTableId = cheapestFreightTableId;
                result.RateTableType = cheapestRateTableType;
                result.CalculatedPrice = minCost;
                result.IsPriceFound = true;
            }
            else
            {
                result.WarehouseId = null;
                result.IsPriceFound = false;
            }

            return result;
        }

        /// <summary>
        /// 最安倉庫のIDのみを返却する補助メソッド
        /// </summary>
        public async Task<Guid?> FindCheapestWarehouseAsync(
            Guid shipperId,
            string productId,
            Guid carrierId,
            string zipCode,
            int totalPieces,
            bool is30KgFixed)
        {
            var res = await FindCheapestWarehouseOptionAsync(shipperId, productId, carrierId, zipCode, totalPieces, is30KgFixed);
            return res.WarehouseId;
        }

        /// <summary>
        /// 出荷データに基づき、指定倉庫の在庫からFIFO（先入先出）ルールで在庫の引き当てを行います。
        /// バラ出荷およびフルケース出荷の在庫コントロールを自動適用します。
        /// </summary>
        /// <param name="outboundId">対象の出荷ID</param>
        /// <param name="warehouseId">引当元倉庫ID</param>
        /// <param name="shipperId">荷主ID</param>
        /// <param name="productId">商品コード</param>
        /// <param name="totalPieces">必要総ピース数</param>
        /// <param name="scheduledDate">出荷予定日時</param>
        /// <returns>計算された出荷箱数（ケース数）</returns>
        public async Task<int> AllocateInventoryAsync(
            Guid outboundId,
            Guid warehouseId,
            Guid shipperId,
            string productId,
            int totalPieces,
            DateTime? scheduledDate)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == productId);
            int unitQuantity = product?.Quantity > 0 ? product.Quantity : 1;

            int fullCaseNeeded = totalPieces / unitQuantity;
            int looseNeeded = totalPieces % unitQuantity;

            var allocations = new List<OutboundAllocation>();

            // 1. バラ数量の引き当て（既存の端数バラ箱を優先して引当。足りない場合は未開封ケースを開封）
            if (looseNeeded > 0)
            {
                var looseInventories = await _context.Inventories
                    .Where(i => i.ShipperId == shipperId && i.WarehouseId == warehouseId && i.ProductId == productId && i.Status == 1 && i.IsLoose && i.CurrentQuantity > 0 && !i.IsDeleted)
                    .OrderBy(i => i.ActualInboundDate)
                    .ThenBy(i => i.CreatedAt)
                    .ToListAsync();

                int looseRemainingToFill = looseNeeded;

                foreach (var looseInv in looseInventories)
                {
                    if (looseRemainingToFill <= 0) break;

                    int takeQty = Math.Min(looseInv.CurrentQuantity, looseRemainingToFill);
                    looseInv.CurrentQuantity -= takeQty;
                    looseRemainingToFill -= takeQty;

                    allocations.Add(new OutboundAllocation
                    {
                        AllocationId = Guid.NewGuid(),
                        OutboundId = outboundId,
                        InventoryId = looseInv.InventoryId,
                        AllocatedQuantity = takeQty,
                        IsLooseShipment = true,
                        Status = 11 // 引当済
                    });
                }

                // バラ在庫で不足する場合、古い未開封ケースを開封して残りを引き当て
                if (looseRemainingToFill > 0)
                {
                    var unopenedInventories = await _context.Inventories
                        .Where(i => i.ShipperId == shipperId && i.WarehouseId == warehouseId && i.ProductId == productId && i.Status == 1 && !i.IsLoose && !i.IsDeleted)
                        .OrderBy(i => i.ActualInboundDate)
                        .ThenBy(i => i.CreatedAt)
                        .ToListAsync();

                    while (looseRemainingToFill > 0 && unopenedInventories.Any())
                    {
                        var unopenedInv = unopenedInventories.First();
                        unopenedInventories.RemoveAt(0);

                        unopenedInv.IsLoose = true; // 開封フラグON
                        int takeQty = Math.Min(unopenedInv.CurrentQuantity, looseRemainingToFill);
                        unopenedInv.CurrentQuantity -= takeQty;
                        looseRemainingToFill -= takeQty;

                        allocations.Add(new OutboundAllocation
                        {
                            AllocationId = Guid.NewGuid(),
                            OutboundId = outboundId,
                            InventoryId = unopenedInv.InventoryId,
                            AllocatedQuantity = takeQty,
                            IsLooseShipment = true,
                            Status = 11
                        });
                    }
                }
            }

            // 2. フルケース数量の引き当て（未開封ケースをそのまま引当）
            if (fullCaseNeeded > 0)
            {
                var unopenedInventories = await _context.Inventories
                    .Where(i => i.ShipperId == shipperId && i.WarehouseId == warehouseId && i.ProductId == productId && i.Status == 1 && !i.IsLoose && !i.IsDeleted)
                    .OrderBy(i => i.ActualInboundDate)
                    .ThenBy(i => i.CreatedAt)
                    .Take(fullCaseNeeded)
                    .ToListAsync();

                foreach (var fullInv in unopenedInventories)
                {
                    int takeQty = fullInv.CurrentQuantity;
                    fullInv.Status = 11; // フルケースはステータスを引当済（11）に変更
                    fullInv.ScheduledOutboundDate = scheduledDate;

                    allocations.Add(new OutboundAllocation
                    {
                        AllocationId = Guid.NewGuid(),
                        OutboundId = outboundId,
                        InventoryId = fullInv.InventoryId,
                        AllocatedQuantity = takeQty,
                        IsLooseShipment = false,
                        Status = 11
                    });
                }
            }

            _context.OutboundAllocations.AddRange(allocations);
            await _context.SaveChangesAsync();

            // ケース数(出荷箱数) = フルケース数 + (バラ部数 > 0 ? 1 : 0)
            int calculatedCaseCount = fullCaseNeeded + (looseNeeded > 0 ? 1 : 0);
            return calculatedCaseCount;
        }

        /// <summary>
        /// 出荷キャンセルや条件変更時に、過去に行った在庫の引当を解除して在庫数を元に戻します。
        /// </summary>
        /// <param name="outboundId">対象の出荷ID</param>
        public async Task ReleaseInventoryAllocationAsync(Guid outboundId)
        {
            var allocations = await _context.OutboundAllocations
                .Include(a => a.Inventory)
                    .ThenInclude(i => i!.Product)
                .Where(a => a.OutboundId == outboundId && !a.IsDeleted)
                .ToListAsync();

            var affectedInventories = new HashSet<Inventory>();

            foreach (var alloc in allocations)
            {
                alloc.IsDeleted = true; // 引当明細を論理削除
                if (alloc.Inventory != null)
                {
                    if (alloc.IsLooseShipment)
                    {
                        // バラ引き当て分の数量を在庫に返却
                        alloc.Inventory.CurrentQuantity += alloc.AllocatedQuantity;
                        affectedInventories.Add(alloc.Inventory);
                    }
                    else
                    {
                        // フルケース引当分のステータスを保管中（1）に復元
                        alloc.Inventory.Status = 1;
                        alloc.Inventory.ScheduledOutboundDate = null;
                        affectedInventories.Add(alloc.Inventory);
                    }
                }
            }

            // 残部数がケース入数(Product.Quantity)以上に復元されたバラ箱は、未開封ケース(IsLoose = false)に状態復元
            foreach (var inv in affectedInventories)
            {
                int unitQuantity = inv.Product?.Quantity > 0 ? inv.Product.Quantity : 1;
                if (inv.IsLoose && inv.CurrentQuantity >= unitQuantity)
                {
                    inv.IsLoose = false;
                    inv.Status = 1;
                    inv.ScheduledOutboundDate = null;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
