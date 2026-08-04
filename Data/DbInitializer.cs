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

            // 1. スキーママイグレーション補正処理（SQL Server環境の場合のみ実行）
            if (context.Database.IsSqlServer())
            {
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
                            [project_id] uniqueidentifier NULL,
                            [carrier_id] uniqueidentifier NULL,
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
                            CONSTRAINT [FK_t_shipping_instruction_t_project_project_id] FOREIGN KEY ([project_id]) REFERENCES [t_project] ([project_id]),
                            CONSTRAINT [FK_t_shipping_instruction_m_carrier_carrier_id] FOREIGN KEY ([carrier_id]) REFERENCES [m_carrier] ([carrier_id])
                        );
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('t_shipping_instruction') AND name = 'project_id')
                        BEGIN
                            ALTER TABLE [t_shipping_instruction] ADD [project_id] uniqueidentifier NULL;
                            ALTER TABLE [t_shipping_instruction] ADD CONSTRAINT [FK_t_shipping_instruction_t_project_project_id] FOREIGN KEY ([project_id]) REFERENCES [t_project] ([project_id]);
                        END
                        ALTER TABLE [t_shipping_instruction] ALTER COLUMN [carrier_id] uniqueidentifier NULL;
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

                    -- 旧廃止テーブル (m_warehouse_distance_rate) の完全物理削除 (DROP)
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('m_warehouse_distance_rate') AND type = 'U')
                    BEGIN
                        DROP TABLE [m_warehouse_distance_rate];
                    END

                    -- 案件テーブル旧名 (m_project) のリネーム移行 (m_ -> t_)
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('m_project_warehouse_freight_table') AND type = 'U')
                    BEGIN
                        EXEC sp_rename 'm_project_warehouse_freight_table', 't_project_warehouse_freight_table';
                    END
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('m_project_warehouse') AND type = 'U')
                    BEGIN
                        EXEC sp_rename 'm_project_warehouse', 't_project_warehouse';
                    END
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('m_project') AND type = 'U')
                    BEGIN
                        EXEC sp_rename 'm_project', 't_project';
                    END

                    -- 案件管理テーブル (t_project) の作成
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('t_project') AND type = 'U')
                    BEGIN
                        CREATE TABLE [t_project] (
                            [project_id] uniqueidentifier NOT NULL,
                            [shipper_id] uniqueidentifier NOT NULL,
                            [project_code] varchar(32) NULL,
                            [project_name] nvarchar(64) NOT NULL,
                            [remarks] nvarchar(256) NULL,
                            [is_deleted] bit NOT NULL DEFAULT 0,
                            [created_by] nvarchar(64) NULL,
                            [created_at] datetime2 NULL,
                            [updated_by] nvarchar(64) NULL,
                            [updated_at] datetime2 NULL,
                            CONSTRAINT [PK_t_project] PRIMARY KEY ([project_id]),
                            CONSTRAINT [FK_t_project_m_shipper_shipper_id] FOREIGN KEY ([shipper_id]) REFERENCES [m_shipper] ([shipper_id])
                        );
                    END

                    -- 案件利用倉庫テーブル (t_project_warehouse) の作成
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('t_project_warehouse') AND type = 'U')
                    BEGIN
                        CREATE TABLE [t_project_warehouse] (
                            [project_id] uniqueidentifier NOT NULL,
                            [warehouse_id] uniqueidentifier NOT NULL,
                            [is_deleted] bit NOT NULL DEFAULT 0,
                            [created_by] nvarchar(64) NULL,
                            [created_at] datetime2 NULL,
                            [updated_by] nvarchar(64) NULL,
                            [updated_at] datetime2 NULL,
                            CONSTRAINT [PK_t_project_warehouse] PRIMARY KEY ([project_id], [warehouse_id]),
                            CONSTRAINT [FK_t_project_warehouse_t_project_project_id] FOREIGN KEY ([project_id]) REFERENCES [t_project] ([project_id]),
                            CONSTRAINT [FK_t_project_warehouse_m_warehouse_warehouse_id] FOREIGN KEY ([warehouse_id]) REFERENCES [m_warehouse] ([warehouse_id])
                        );
                    END

                    -- 案件倉庫料金表テーブル (t_project_warehouse_freight_table) の作成
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('t_project_warehouse_freight_table') AND type = 'U')
                    BEGIN
                        CREATE TABLE [t_project_warehouse_freight_table] (
                            [project_id] uniqueidentifier NOT NULL,
                            [warehouse_id] uniqueidentifier NOT NULL,
                            [freight_table_id] uniqueidentifier NOT NULL,
                            [is_deleted] bit NOT NULL DEFAULT 0,
                            [created_by] nvarchar(64) NULL,
                            [created_at] datetime2 NULL,
                            [updated_by] nvarchar(64) NULL,
                            [updated_at] datetime2 NULL,
                            CONSTRAINT [PK_t_project_warehouse_freight_table] PRIMARY KEY ([project_id], [warehouse_id], [freight_table_id]),
                            CONSTRAINT [FK_t_project_warehouse_freight_table_t_project_project_id] FOREIGN KEY ([project_id]) REFERENCES [t_project] ([project_id]),
                            CONSTRAINT [FK_t_project_warehouse_freight_table_m_warehouse_warehouse_id] FOREIGN KEY ([warehouse_id]) REFERENCES [m_warehouse] ([warehouse_id]),
                            CONSTRAINT [FK_t_project_warehouse_freight_table_m_freight_table_freight_table_id] FOREIGN KEY ([freight_table_id]) REFERENCES [m_freight_table] ([freight_table_id])
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
            }
            else if (context.Database.IsSqlite())
            {
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS [m_carrier] ([carrier_id] TEXT NOT NULL PRIMARY KEY, [carrier_name] TEXT NOT NULL, [is_deleted] INTEGER NOT NULL DEFAULT 0, [created_by] TEXT NULL, [created_at] TEXT NULL, [updated_by] TEXT NULL, [updated_at] TEXT NULL);
                        CREATE TABLE IF NOT EXISTS [m_freight_table] ([freight_table_id] TEXT NOT NULL PRIMARY KEY, [rate_name] TEXT NOT NULL, [rate_table_type] INTEGER NOT NULL, [carrier_id] TEXT NULL, [is_deleted] INTEGER NOT NULL DEFAULT 0, [created_by] TEXT NULL, [created_at] TEXT NULL, [updated_by] TEXT NULL, [updated_at] TEXT NULL);
                        CREATE TABLE IF NOT EXISTS [m_individual_freight] ([individual_freight_id] TEXT NOT NULL PRIMARY KEY, [freight_table_id] TEXT NOT NULL, [pref_code] TEXT NOT NULL, [pref_name] TEXT NOT NULL, [cost] INTEGER NOT NULL, [price] INTEGER NOT NULL, [is_deleted] INTEGER NOT NULL DEFAULT 0, [created_by] TEXT NULL, [created_at] TEXT NULL, [updated_by] TEXT NULL, [updated_at] TEXT NULL);
                        CREATE TABLE IF NOT EXISTS [m_shipping_class] ([shipping_class_id] TEXT NOT NULL PRIMARY KEY, [class_name] TEXT NOT NULL, [rate_table_type] INTEGER NOT NULL, [carrier_id] TEXT NULL, [is_deleted] INTEGER NOT NULL DEFAULT 0, [created_by] TEXT NULL, [created_at] TEXT NULL, [updated_by] TEXT NULL, [updated_at] TEXT NULL);
                        CREATE TABLE IF NOT EXISTS [m_warehouse_distance_rate] ([warehouse_id] TEXT NOT NULL, [freight_table_id] TEXT NOT NULL, [is_deleted] INTEGER NOT NULL DEFAULT 0, [created_by] TEXT NULL, [created_at] TEXT NULL, [updated_by] TEXT NULL, [updated_at] TEXT NULL, PRIMARY KEY ([warehouse_id], [freight_table_id]));
                        CREATE TABLE IF NOT EXISTS [t_shipping_instruction] ([shipping_instruction_id] TEXT NOT NULL PRIMARY KEY, [shipping_instruction_group] TEXT NOT NULL, [file_name] TEXT NULL, [file_size] INTEGER NOT NULL DEFAULT 0, [shipper_id] TEXT NOT NULL, [carrier_id] TEXT NOT NULL, [weight_spec] TEXT NULL, [imported_count] INTEGER NOT NULL DEFAULT 0, [status] INTEGER NOT NULL DEFAULT 1, [is_deleted] INTEGER NOT NULL DEFAULT 0, [created_by] TEXT NULL, [created_at] TEXT NULL, [updated_by] TEXT NULL, [updated_at] TEXT NULL);
                        CREATE TABLE IF NOT EXISTS [t_outbound_allocation] ([allocation_id] TEXT NOT NULL PRIMARY KEY, [outbound_id] TEXT NOT NULL, [inventory_id] TEXT NOT NULL, [allocated_quantity] INTEGER NOT NULL, [is_loose_shipment] INTEGER NOT NULL DEFAULT 0, [status] INTEGER NOT NULL DEFAULT 11, [is_deleted] INTEGER NOT NULL DEFAULT 0, [created_by] TEXT NULL, [created_at] TEXT NULL, [updated_by] TEXT NULL, [updated_at] TEXT NULL);
                    ");

                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_individual_freight] ADD COLUMN [size] INTEGER NOT NULL DEFAULT 0;"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_individual_freight] ADD COLUMN [weight] INTEGER NOT NULL DEFAULT 0;"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_shipping_class] ADD COLUMN [rate_table_type] INTEGER NOT NULL DEFAULT 1;"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_shipping_class] ADD COLUMN [carrier_id] TEXT NULL;"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_freight_table] ADD COLUMN [carrier_id] TEXT NULL;"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_warehouse_distance_rate] ADD COLUMN [freight_table_id] TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_warehouse_distance_rate] ADD COLUMN [warehouse_id] TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_distance_freight] ADD COLUMN [freight_table_id] TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_distance_freight] ADD COLUMN [size] INTEGER NOT NULL DEFAULT 0;"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [m_distance_freight] ADD COLUMN [weight] INTEGER NOT NULL DEFAULT 0;"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [t_inventory] ADD COLUMN [current_quantity] INTEGER NOT NULL DEFAULT 0;"); } catch {}
                    try { context.Database.ExecuteSqlRaw("ALTER TABLE [t_inventory] ADD COLUMN [is_loose] INTEGER NOT NULL DEFAULT 0;"); } catch {}
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SQLite Schema Check] {ex.Message}");
                }
            }

            // 2. ロール・権限管理用テーブル自律作成 & カラム拡張
            if (context.Database.IsSqlServer())
            {
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        -- t_account への display_name, is_active 追加
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('t_account') AND name = 'display_name')
                        BEGIN
                            ALTER TABLE [t_account] ADD [display_name] nvarchar(64) NULL;
                        END

                        UPDATE [t_account] SET [display_name] = N'システム管理者' WHERE [display_name] IS NULL;

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('t_account') AND name = 'is_active')
                        BEGIN
                            ALTER TABLE [t_account] ADD [is_active] bit NOT NULL DEFAULT 1;
                        END

                        -- t_role テーブル作成
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('t_role') AND type = 'U')
                        BEGIN
                            CREATE TABLE [t_role] (
                                [role_id] int IDENTITY(1,1) NOT NULL,
                                [role_code] varchar(32) NOT NULL,
                                [role_name] nvarchar(64) NOT NULL,
                                [description] nvarchar(256) NULL,
                                [is_deleted] bit NOT NULL DEFAULT 0,
                                [created_by] varchar(64) NULL,
                                [created_at] datetime2 NULL,
                                [updated_by] varchar(64) NULL,
                                [updated_at] datetime2 NULL,
                                CONSTRAINT [PK_t_role] PRIMARY KEY ([role_id]),
                                CONSTRAINT [UQ_t_role_code] UNIQUE ([role_code])
                            );
                        END

                        -- t_permission テーブル作成
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('t_permission') AND type = 'U')
                        BEGIN
                            CREATE TABLE [t_permission] (
                                [permission_id] int IDENTITY(1,1) NOT NULL,
                                [permission_code] varchar(64) NOT NULL,
                                [category] nvarchar(64) NOT NULL,
                                [permission_name] nvarchar(64) NOT NULL,
                                [description] nvarchar(256) NULL,
                                [is_deleted] bit NOT NULL DEFAULT 0,
                                [created_by] varchar(64) NULL,
                                [created_at] datetime2 NULL,
                                [updated_by] varchar(64) NULL,
                                [updated_at] datetime2 NULL,
                                CONSTRAINT [PK_t_permission] PRIMARY KEY ([permission_id]),
                                CONSTRAINT [UQ_t_permission_code] UNIQUE ([permission_code])
                            );
                        END

                        -- t_account_role テーブル作成
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('t_account_role') AND type = 'U')
                        BEGIN
                            CREATE TABLE [t_account_role] (
                                [account_name] varchar(32) NOT NULL,
                                [role_id] int NOT NULL,
                                CONSTRAINT [PK_t_account_role] PRIMARY KEY ([account_name], [role_id]),
                                CONSTRAINT [FK_t_account_role_t_account] FOREIGN KEY ([account_name]) REFERENCES [t_account] ([account_name]),
                                CONSTRAINT [FK_t_account_role_t_role] FOREIGN KEY ([role_id]) REFERENCES [t_role] ([role_id])
                            );
                        END

                        -- t_role_permission テーブル作成
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('t_role_permission') AND type = 'U')
                        BEGIN
                            CREATE TABLE [t_role_permission] (
                                [role_id] int NOT NULL,
                                [permission_id] int NOT NULL,
                                CONSTRAINT [PK_t_role_permission] PRIMARY KEY ([role_id], [permission_id]),
                                CONSTRAINT [FK_t_role_permission_t_role] FOREIGN KEY ([role_id]) REFERENCES [t_role] ([role_id]),
                                CONSTRAINT [FK_t_role_permission_t_permission] FOREIGN KEY ([permission_id]) REFERENCES [t_permission] ([permission_id])
                            );
                        END
                    ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Schema ERROR] Role/Permission table creation failed: {ex.Message}");
                }
            }

            // 3. ロール・パーミッション初期シードデータの自動投入
            SeedRolesAndPermissions(context);

            // 4. 初期管理者ユーザー（WMSAdmin）の作成・パスワードハッシュ化移行 & ロール紐付け
            var adminAccount = context.Accounts.IgnoreQueryFilters().FirstOrDefault(a => a.AccountName == "WMSAdmin");
            if (adminAccount == null)
            {
                adminAccount = new Account
                {
                    AccountName = "WMSAdmin",
                    DisplayName = "システム管理者",
                    Password = PasswordHelper.HashPassword("abc123$%&"),
                    Role = 0,
                    IsActive = true,
                    CreatedBy = "SYSTEM",
                    CreatedAt = DateTime.Now,
                    UpdatedBy = "SYSTEM",
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false
                };
                context.Accounts.Add(adminAccount);
                context.SaveChanges();
            }
            else
            {
                adminAccount.Password = PasswordHelper.HashPassword("abc123$%&");
                if (string.IsNullOrEmpty(adminAccount.DisplayName)) adminAccount.DisplayName = "システム管理者";
                adminAccount.IsActive = true;
                context.SaveChanges();
            }

            // 初期管理者に SystemAdmin ロールを割り当て
            var sysAdminRole = context.Roles.FirstOrDefault(r => r.RoleCode == "SystemAdmin");
            if (sysAdminRole != null)
            {
                var existingRoleMap = context.AccountRoles.FirstOrDefault(ar => ar.AccountName == "WMSAdmin" && ar.RoleId == sysAdminRole.RoleId);
                if (existingRoleMap == null)
                {
                    context.AccountRoles.Add(new AccountRole
                    {
                        AccountName = "WMSAdmin",
                        RoleId = sysAdminRole.RoleId
                    });
                    context.SaveChanges();
                }
            }
        }

        /// <summary>
        /// 初期ロール・パーミッションおよび紐付けデータの自動作成
        /// </summary>
        private static void SeedRolesAndPermissions(WmsDbContext context)
        {
            var now = DateTime.Now;

            // 1. パーミッション定義のシードデータ
            var defaultPermissions = new List<Permission>
            {
                new Permission { Category = "システム管理", PermissionCode = "UserManagement:Manage", PermissionName = "ユーザー・権限管理", Description = "ユーザー作成・ロール・権限の設定権限" },
                new Permission { Category = "マスター管理", PermissionCode = "Master:View", PermissionName = "マスター参照", Description = "全マスターの閲覧権限" },
                new Permission { Category = "マスター管理", PermissionCode = "Master:Edit", PermissionName = "マスター編集", Description = "マスターの作成・変更・削除権限" },
                new Permission { Category = "マスター管理", PermissionCode = "Master:Import", PermissionName = "マスターインポート", Description = "CSV/Excel一括インポート権限" },
                new Permission { Category = "入荷業務", PermissionCode = "Inbound:View", PermissionName = "入荷参照", Description = "入荷予定・実績の照会権限" },
                new Permission { Category = "入荷業務", PermissionCode = "Inbound:Edit", PermissionName = "入荷編集・検品", Description = "入荷予定作成・検品登録権限" },
                new Permission { Category = "出荷業務", PermissionCode = "Outbound:View", PermissionName = "出荷参照", Description = "出荷指示・実績の照会権限" },
                new Permission { Category = "出荷業務", PermissionCode = "Outbound:Edit", PermissionName = "出荷作業・編集", Description = "出荷指示登録・引当・作業権限" },
                new Permission { Category = "出荷業務", PermissionCode = "Outbound:Ship", PermissionName = "出荷確定", Description = "出荷確定処理権限" },
                new Permission { Category = "在庫・棚卸", PermissionCode = "Stock:View", PermissionName = "在庫参照", Description = "在庫一覧・履歴照会権限" },
                new Permission { Category = "在庫・棚卸", PermissionCode = "Stock:Adjust", PermissionName = "在庫調整・棚卸", Description = "在庫調整・棚卸確定権限" }
            };

            foreach (var perm in defaultPermissions)
            {
                var existing = context.Permissions.IgnoreQueryFilters().FirstOrDefault(p => p.PermissionCode == perm.PermissionCode);
                if (existing == null)
                {
                    perm.CreatedBy = "SYSTEM";
                    perm.CreatedAt = now;
                    perm.UpdatedBy = "SYSTEM";
                    perm.UpdatedAt = now;
                    context.Permissions.Add(perm);
                }
            }
            context.SaveChanges();

            // 2. ロール定義のシードデータ
            var defaultRoles = new List<Role>
            {
                new Role { RoleCode = "SystemAdmin", RoleName = "システム管理者", Description = "すべての機能およびユーザー・権限管理を実行可能" },
                new Role { RoleCode = "WarehouseManager", RoleName = "倉庫管理者", Description = "マスター編集・入出荷・在庫調整を含む倉庫全般の運用権限" },
                new Role { RoleCode = "Operator", RoleName = "現場作業員", Description = "日常の入検品・出検品作業および在庫照会権限" },
                new Role { RoleCode = "Viewer", RoleName = "閲覧専用", Description = "すべてのマスター・状況照会画面の参照のみ" }
            };

            foreach (var role in defaultRoles)
            {
                var existing = context.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.RoleCode == role.RoleCode);
                if (existing == null)
                {
                    role.CreatedBy = "SYSTEM";
                    role.CreatedAt = now;
                    role.UpdatedBy = "SYSTEM";
                    role.UpdatedAt = now;
                    context.Roles.Add(role);
                }
            }
            context.SaveChanges();

            // 3. ロールとパーミッションの自動マッピング
            var allPermissions = context.Permissions.IgnoreQueryFilters().ToList();
            var roles = context.Roles.IgnoreQueryFilters().ToList();

            foreach (var role in roles)
            {
                List<string> targetCodes = role.RoleCode switch
                {
                    "SystemAdmin" => allPermissions.Select(p => p.PermissionCode).ToList(),
                    "WarehouseManager" => allPermissions.Where(p => p.PermissionCode != "UserManagement:Manage").Select(p => p.PermissionCode).ToList(),
                    "Operator" => new List<string> { "Master:View", "Inbound:View", "Inbound:Edit", "Outbound:View", "Outbound:Edit", "Outbound:Ship", "Stock:View", "Stock:Adjust" },
                    "Viewer" => allPermissions.Where(p => p.PermissionCode.EndsWith(":View")).Select(p => p.PermissionCode).ToList(),
                    _ => new List<string>()
                };

                foreach (var code in targetCodes)
                {
                    var perm = allPermissions.FirstOrDefault(p => p.PermissionCode == code);
                    if (perm != null)
                    {
                        var rpExists = context.RolePermissions.Any(rp => rp.RoleId == role.RoleId && rp.PermissionId == perm.PermissionId);
                        if (!rpExists)
                        {
                            context.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = perm.PermissionId });
                        }
                    }
                }
            }
            context.SaveChanges();
        }
    }
}
