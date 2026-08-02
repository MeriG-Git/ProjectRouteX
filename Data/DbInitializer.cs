using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RouteXWms.Models;
using RouteXWms.Services;

namespace RouteXWms.Data
{
    /// <summary>
    /// データベース初期化クラス
    /// データベースの自動構築、テーブルスキーマの変更（マイグレーション補正）、初期ユーザーおよび初期マスターデータの投入を行います。
    /// </summary>
    public static class DbInitializer
    {
        /// <summary>
        /// データベースの初期化とシードデータの投入を実行します。
        /// </summary>
        /// <param name="context">DbContextインスタンス</param>
        public static void Initialize(WmsDbContext context)
        {
            // データベースが存在しない場合は作成
            context.Database.EnsureCreated();

            // 1. スキーママイグレーション補正処理（型変更および欠落カラム/テーブルの追加）
            try
            {
                // 距離別運賃のコスト・価格カラムの型をintに補正
                context.Database.ExecuteSqlRaw(@"
                    IF EXISTS (
                        SELECT * FROM sys.columns 
                        WHERE object_id = OBJECT_ID('m_distance_freight') 
                          AND name = 'cost' 
                          AND TYPE_NAME(system_type_id) <> 'int'
                    )
                    BEGIN
                        ALTER TABLE [m_distance_freight] ALTER COLUMN [cost] int NOT NULL;
                        ALTER TABLE [m_distance_freight] ALTER COLUMN [price] int NOT NULL;
                    END
                ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Migration ERROR] Cost/Price column alter failed: {ex.Message}");
            }

            try
            {
                context.Database.ExecuteSqlRaw(@"
                    -- 郵便番号マスターの桁数拡張および制約補正
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('m_zip_code') AND name = 'zip_code' AND max_length < 7)
                    BEGIN
                        DECLARE @zip_pk_name nvarchar(128);
                        SELECT @zip_pk_name = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('m_zip_code');
                        IF @zip_pk_name IS NOT NULL
                        BEGIN
                            EXEC('ALTER TABLE [m_zip_code] DROP CONSTRAINT [' + @zip_pk_name + ']');
                        END

                        ALTER TABLE [m_zip_code] ALTER COLUMN [zip_code] varchar(7) NOT NULL;
                        ALTER TABLE [m_zip_code] ADD CONSTRAINT [PK_m_zip_code] PRIMARY KEY CLUSTERED ([zip_code]);
                    END

                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('m_zip_code') AND name = 'pref_code')
                    BEGIN
                        ALTER TABLE [m_zip_code] ALTER COLUMN [pref_code] varchar(2) NOT NULL;
                        ALTER TABLE [m_zip_code] ALTER COLUMN [city_code] varchar(5) NOT NULL;
                    END

                    -- 運賃表マスターへの運送会社ID追加
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('m_freight_table') AND type = 'U')
                       AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('m_freight_table') AND name = 'carrier_id')
                    BEGIN
                        ALTER TABLE [m_freight_table] ADD [carrier_id] uniqueidentifier NULL;
                    END

                    -- 在庫テーブルの古いカラム削除と新カラム追加
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('t_inventory') AND name = 'outbound_id')
                    BEGIN
                        DECLARE @fk_inv_out nvarchar(128);
                        SELECT @fk_inv_out = name FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('t_inventory') AND referenced_object_id = OBJECT_ID('t_outbound');
                        IF @fk_inv_out IS NOT NULL
                        BEGIN
                            EXEC('ALTER TABLE [t_inventory] DROP CONSTRAINT [' + @fk_inv_out + ']');
                        END
                        ALTER TABLE [t_inventory] DROP COLUMN [outbound_id];
                    END

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('t_inventory') AND name = 'current_quantity')
                    BEGIN
                        ALTER TABLE [t_inventory] ADD [current_quantity] int NOT NULL DEFAULT 0;
                    END

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('t_inventory') AND name = 'is_loose')
                    BEGIN
                        ALTER TABLE [t_inventory] ADD [is_loose] bit NOT NULL DEFAULT 0;
                    END

                    -- 出荷指示テーブルの作成
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('t_shipping_instruction') AND type = 'U')
                    BEGIN
                        CREATE TABLE [t_shipping_instruction] (
                            [shipping_instruction_id] uniqueidentifier NOT NULL,
                            [shipping_instruction_group] varchar(64) NOT NULL,
                            [file_name] nvarchar(256) NULL,
                            [file_size] bigint NOT NULL DEFAULT 0,
                            [shipper_id] uniqueidentifier NOT NULL,
                            [carrier_id] uniqueidentifier NOT NULL,
                            [weight_spec] varchar(32) NULL,
                            [imported_count] int NOT NULL DEFAULT 0,
                            [status] int NOT NULL DEFAULT 1,
                            [is_deleted] bit NOT NULL DEFAULT 0,
                            [created_by] nvarchar(64) NULL,
                            [created_at] datetime2 NULL,
                            [updated_by] nvarchar(64) NULL,
                            [updated_at] datetime2 NULL,
                            CONSTRAINT [PK_t_shipping_instruction] PRIMARY KEY ([shipping_instruction_id]),
                            CONSTRAINT [FK_t_shipping_instruction_m_shipper_shipper_id] FOREIGN KEY ([shipper_id]) REFERENCES [m_shipper] ([shipper_id]),
                            CONSTRAINT [FK_t_shipping_instruction_m_carrier_carrier_id] FOREIGN KEY ([carrier_id]) REFERENCES [m_carrier] ([carrier_id])
                        );
                    END

                    -- 出荷引当明細テーブルの作成
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('t_outbound_allocation') AND type = 'U')
                    BEGIN
                        CREATE TABLE [t_outbound_allocation] (
                            [allocation_id] uniqueidentifier NOT NULL,
                            [outbound_id] uniqueidentifier NOT NULL,
                            [inventory_id] uniqueidentifier NOT NULL,
                            [allocated_quantity] int NOT NULL,
                            [is_loose_shipment] bit NOT NULL DEFAULT 0,
                            [status] int NOT NULL DEFAULT 11,
                            [is_deleted] bit NOT NULL DEFAULT 0,
                            [created_by] nvarchar(64) NULL,
                            [created_at] datetime2 NULL,
                            [updated_by] nvarchar(64) NULL,
                            [updated_at] datetime2 NULL,
                            CONSTRAINT [PK_t_outbound_allocation] PRIMARY KEY ([allocation_id]),
                            CONSTRAINT [FK_t_outbound_allocation_t_outbound_outbound_id] FOREIGN KEY ([outbound_id]) REFERENCES [t_outbound] ([outbound_id]),
                            CONSTRAINT [FK_t_outbound_allocation_t_inventory_inventory_id] FOREIGN KEY ([inventory_id]) REFERENCES [t_inventory] ([inventory_id])
                        );
                    END
                ");

                // 既存の運賃表マスターへのデフォルト運送会社IDの設定
                var firstCarrier = context.Carriers.IgnoreQueryFilters().FirstOrDefault();
                if (firstCarrier != null)
                {
                    context.Database.ExecuteSqlRaw($"UPDATE [m_freight_table] SET [carrier_id] = '{firstCarrier.CarrierId}' WHERE [carrier_id] IS NULL");
                }
                else
                {
                    context.Database.ExecuteSqlRaw("UPDATE [m_freight_table] SET [carrier_id] = '00000000-0000-0000-0000-000000000000' WHERE [carrier_id] IS NULL");
                }

                context.Database.ExecuteSqlRaw(@"
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('m_freight_table') AND name = 'carrier_id')
                    BEGIN
                        ALTER TABLE [m_freight_table] ALTER COLUMN [carrier_id] uniqueidentifier NOT NULL;
                    END
                ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Migration ERROR] Schema update failed: {ex.Message}");
                Console.WriteLine(ex.ToString());
            }

            // 2. 初期管理者ユーザー（WMSAdmin）の作成・パスワードハッシュ化移行
            var adminAccount = context.Accounts.FirstOrDefault(a => a.AccountName == "WMSAdmin");
            if (adminAccount == null)
            {
                context.Accounts.Add(new Account
                {
                    AccountName = "WMSAdmin",
                    Password = PasswordHelper.HashPassword("abc123$%&"),
                    Role = 0, // 管理者権限
                    CreatedBy = "SYSTEM",
                    CreatedAt = DateTime.Now,
                    UpdatedBy = "SYSTEM",
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false
                });
                context.SaveChanges();
            }
            else
            {
                // プロジェクト名変更に伴うソルト変更に対応するため、初期管理者パスワードを確実に最新ハッシュに更新
                adminAccount.Password = PasswordHelper.HashPassword("abc123$%&");
                context.SaveChanges();
            }

            // 3. サンプルマスターデータの作成（データが空の場合のみ登録）
            if (!context.Shippers.Any())
            {
                // 荷主マスター
                var shipper1 = new Shipper
                {
                    ShipperId = Guid.NewGuid(),
                    ShipperName = "YK商事株式会社",
                    ShipperAddress1 = "東京都千代田区神田1-1-1",
                    ShipperAddress2 = "YKビル 5F",
                    ShipperTel = "03-1234-5678"
                };
                var shipper2 = new Shipper
                {
                    ShipperId = Guid.NewGuid(),
                    ShipperName = "グローバルロジテック",
                    ShipperAddress1 = "大阪府大阪市中央区本町2-2-2",
                    ShipperAddress2 = "本町タワー 10F",
                    ShipperTel = "06-9876-5432"
                };
                context.Shippers.AddRange(shipper1, shipper2);

                // 倉庫マスター
                var wh1 = new Warehouse
                {
                    WarehouseId = Guid.NewGuid(),
                    WarehouseName = "東京第一倉庫",
                    ZipCode = "1000001",
                    Address = "東京都千代田区大手町1-1",
                    Tel = "03-3333-4444"
                };
                var wh2 = new Warehouse
                {
                    WarehouseId = Guid.NewGuid(),
                    WarehouseName = "成田物流センター",
                    ZipCode = "2860101",
                    Address = "千葉県成田市取香500",
                    Tel = "0476-11-2233"
                };
                context.Warehouses.AddRange(wh1, wh2);

                // 商品マスター
                var product1 = new Product
                {
                    ProductId = "PRD00001",
                    ProductName = "プレミアムドリップコーヒー 100P",
                    JanCode = "4901234567890",
                    Length = 30.5m,
                    Width = 20.0m,
                    Height = 15.0m,
                    Weight = 3,
                    Quantity = 200
                };
                var product2 = new Product
                {
                    ProductId = "PRD00002",
                    ProductName = "有機オーガニック紅茶 50P",
                    JanCode = "4901234567891",
                    Length = 25.0m,
                    Width = 18.0m,
                    Height = 12.0m,
                    Weight = 2,
                    Quantity = 100
                };
                context.Products.AddRange(product1, product2);

                // 運送会社マスター
                var carrier1 = new Carrier
                {
                    CarrierId = Guid.NewGuid(),
                    CarrierName = "ヤマト運輸"
                };
                var carrier2 = new Carrier
                {
                    CarrierId = Guid.NewGuid(),
                    CarrierName = "佐川急便"
                };
                context.Carriers.AddRange(carrier1, carrier2);

                // 郵便番号マスター
                var zip1 = new ZipCode { ZipCodeValue = "1000001", PrefCode = "13", CityCode = "13101" };
                var zip2 = new ZipCode { ZipCodeValue = "5300001", PrefCode = "27", CityCode = "27100" };
                var zip3 = new ZipCode { ZipCodeValue = "2860101", PrefCode = "12", CityCode = "12211" };
                context.ZipCodes.AddRange(zip1, zip2, zip3);

                // 運賃表マスター
                var distTable = new FreightTable
                {
                    FreightTableId = Guid.NewGuid(),
                    RateName = "関東圏標準路線運賃表",
                    CarrierId = carrier1.CarrierId,
                    RateTableType = 2 // 路線運賃
                };
                context.FreightTables.Add(distTable);

                // 距離別運賃マスター
                var freight1 = new DistanceFreight
                {
                    FreightId = Guid.NewGuid(),
                    FreightTableId = distTable.FreightTableId,
                    DistanceKm = 15,
                    Size = 2,
                    Cost = 500,
                    Price = 700
                };
                var freight2 = new DistanceFreight
                {
                    FreightId = Guid.NewGuid(),
                    FreightTableId = distTable.FreightTableId,
                    DistanceKm = 500,
                    Size = 3,
                    Cost = 1200,
                    Price = 1600
                };
                context.DistanceFreights.AddRange(freight1, freight2);

                // 距離マスター
                var dist1 = new Distance
                {
                    FreightTableId = distTable.FreightTableId,
                    CityCode = "13101",
                    DistanceKm = 15
                };
                var dist2 = new Distance
                {
                    FreightTableId = distTable.FreightTableId,
                    CityCode = "27100",
                    DistanceKm = 500
                };
                context.Distances.AddRange(dist1, dist2);

                // 倉庫距離掛率マスター
                var whRate = new WarehouseDistanceRate
                {
                    WarehouseId = wh1.WarehouseId,
                    FreightTableId = distTable.FreightTableId
                };
                context.WarehouseDistanceRates.Add(whRate);

                context.SaveChanges();
            }

            // 4. 47都道府県の個配運賃マスター初期設定
            var indFreightTables = context.FreightTables.Where(f => f.RateTableType == 1 && !f.IsDeleted).ToList();
            if (!indFreightTables.Any())
            {
                var carrier = context.Carriers.FirstOrDefault();
                var indFreightTable = new FreightTable
                {
                    FreightTableId = Guid.NewGuid(),
                    RateName = "個配標準運賃表",
                    RateTableType = 1, // 個配運賃
                    CarrierId = carrier?.CarrierId ?? Guid.NewGuid()
                };
                context.FreightTables.Add(indFreightTable);
                context.SaveChanges();
                indFreightTables.Add(indFreightTable);
            }

            var prefList = new (string code, string name, int cost, int price)[]
            {
                ("01", "北海道", 730, 920), ("02", "青森県", 530, 650), ("03", "岩手県", 530, 650), ("04", "宮城県", 470, 620),
                ("05", "秋田県", 530, 650), ("06", "山形県", 470, 620), ("07", "福島県", 470, 620), ("08", "茨城県", 470, 620),
                ("09", "栃木県", 470, 620), ("10", "群馬県", 470, 620), ("11", "埼玉県", 470, 620), ("12", "千葉県", 470, 620),
                ("13", "東京都", 470, 620), ("14", "神奈川県", 470, 620), ("15", "新潟県", 470, 620), ("16", "富山県", 470, 620),
                ("17", "石川県", 470, 620), ("18", "福井県", 470, 620), ("19", "山梨県", 470, 620), ("20", "長野県", 470, 620),
                ("21", "岐阜県", 470, 620), ("22", "静岡県", 470, 620), ("23", "愛知県", 470, 620), ("24", "三重県", 470, 620),
                ("25", "滋賀県", 540, 650), ("26", "京都府", 540, 650), ("27", "大阪府", 540, 650), ("28", "兵庫県", 540, 650),
                ("29", "奈良県", 540, 650), ("30", "和歌山県", 540, 650), ("31", "鳥取県", 600, 720), ("32", "島根県", 600, 720),
                ("33", "岡山県", 600, 720), ("34", "広島県", 600, 720), ("35", "山口県", 600, 720), ("36", "徳島県", 670, 820),
                ("37", "香川県", 670, 820), ("38", "愛媛県", 670, 820), ("39", "高知県", 670, 820), ("40", "福岡県", 670, 920),
                ("41", "佐賀県", 730, 920), ("42", "長崎県", 730, 920), ("43", "熊本県", 800, 920), ("44", "大分県", 730, 920),
                ("45", "宮崎県", 800, 920), ("46", "鹿児島県", 800, 920), ("47", "沖縄県", 0, 3150)
            };

            foreach (var table in indFreightTables)
            {
                var existingCodes = context.IndividualFreights
                    .Where(i => i.FreightTableId == table.FreightTableId && !i.IsDeleted)
                    .Select(i => i.PrefCode)
                    .ToHashSet();

                var newFreights = new List<IndividualFreight>();
                foreach (var pref in prefList)
                {
                    if (!existingCodes.Contains(pref.code))
                    {
                        newFreights.Add(new IndividualFreight
                        {
                            IndividualFreightId = Guid.NewGuid(),
                            FreightTableId = table.FreightTableId,
                            PrefCode = pref.code,
                            PrefName = pref.name,
                            Cost = pref.cost,
                            Price = pref.price,
                            Size = 0,
                            Weight = 0
                        });
                    }
                }
                if (newFreights.Any())
                {
                    context.IndividualFreights.AddRange(newFreights);
                }
            }
            context.SaveChanges();
        }
    }
}
