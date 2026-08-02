using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RouteXWms.Models;

namespace RouteXWms.Data
{
    /// <summary>
    /// Project RouteXシステムのデータベースコンテキストクラス
    /// エンティティのデータセット定義、モデルマッピング、論理削除・自動監査情報の自動適用を行います。
    /// </summary>
    public class WmsDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="options">DbContextオプション</param>
        /// <param name="httpContextAccessor">セッションユーザー取得用HTTPコンテキストアクセサ</param>
        public WmsDbContext(DbContextOptions<WmsDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        #region DbSet 定義（各テーブルへのアクセスプロパティ）
        /// <summary>アカウント（ユーザー）データセット</summary>
        public DbSet<Account> Accounts { get; set; } = null!;
        /// <summary>荷主マスターデータセット</summary>
        public DbSet<Shipper> Shippers { get; set; } = null!;
        /// <summary>倉庫マスターデータセット</summary>
        public DbSet<Warehouse> Warehouses { get; set; } = null!;
        /// <summary>商品マスターデータセット</summary>
        public DbSet<Product> Products { get; set; } = null!;
        /// <summary>郵便番号マスターデータセット</summary>
        public DbSet<ZipCode> ZipCodes { get; set; } = null!;
        /// <summary>集荷エリアマスターデータセット</summary>
        public DbSet<CollectionArea> CollectionAreas { get; set; } = null!;
        /// <summary>出荷区分マスターデータセット</summary>
        public DbSet<ShippingClass> ShippingClasses { get; set; } = null!;
        /// <summary>運送会社マスターデータセット</summary>
        public DbSet<Carrier> Carriers { get; set; } = null!;
        /// <summary>運賃表マスターデータセット</summary>
        public DbSet<FreightTable> FreightTables { get; set; } = null!;
        /// <summary>距離別運賃マスターデータセット</summary>
        public DbSet<DistanceFreight> DistanceFreights { get; set; } = null!;
        /// <summary>倉庫距離掛率マスターデータセット</summary>
        public DbSet<WarehouseDistanceRate> WarehouseDistanceRates { get; set; } = null!;
        /// <summary>距離マスターデータセット</summary>
        public DbSet<Distance> Distances { get; set; } = null!;
        /// <summary>個別運賃マスターデータセット</summary>
        public DbSet<IndividualFreight> IndividualFreights { get; set; } = null!;
        /// <summary>入荷データセット</summary>
        public DbSet<Inbound> Inbounds { get; set; } = null!;
        /// <summary>在庫データセット</summary>
        public DbSet<Inventory> Inventories { get; set; } = null!;
        /// <summary>出荷指示データセット</summary>
        public DbSet<ShippingInstruction> ShippingInstructions { get; set; } = null!;
        /// <summary>出荷データセット</summary>
        public DbSet<Outbound> Outbounds { get; set; } = null!;
        /// <summary>出荷引当明細データセット</summary>
        public DbSet<OutboundAllocation> OutboundAllocations { get; set; } = null!;
        #endregion

        /// <summary>
        /// データベースモデルの構築および制約・グローバルフィルターの設定
        /// </summary>
        /// <param name="modelBuilder">モデルビルダー</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 複合主キーの構成
            // 倉庫距離掛率マスター（倉庫ID × 運賃表ID）
            modelBuilder.Entity<WarehouseDistanceRate>()
                .HasKey(w => new { w.WarehouseId, w.FreightTableId });

            // 距離マスター（運賃表ID × 市区町村コード）
            modelBuilder.Entity<Distance>()
                .HasKey(d => new { d.FreightTableId, d.CityCode });

            // 外部キー削除時の連鎖削除（カスケード削除）の禁止設定（参照整合性保護）
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // 論理削除（IsDeleted == false）のグローバルクエリフィルター設定
            modelBuilder.Entity<Account>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Shipper>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Warehouse>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Product>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<ZipCode>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<CollectionArea>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<ShippingClass>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Carrier>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<FreightTable>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<DistanceFreight>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<WarehouseDistanceRate>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Distance>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<IndividualFreight>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Inbound>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Inventory>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<ShippingInstruction>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Outbound>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<OutboundAllocation>().HasQueryFilter(e => !e.IsDeleted);
        }

        /// <summary>
        /// 同期的な変更保存処理（自動監査ログ・論理削除を事前適用）
        /// </summary>
        public override int SaveChanges()
        {
            ApplyAuditAndSoftDelete();
            return base.SaveChanges();
        }

        /// <summary>
        /// 非同期的な変更保存処理（自動監査ログ・論理削除を事前適用）
        /// </summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditAndSoftDelete();
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// エンティティの追加・更新・削除時に自動的に作成者、作成日時、更新者、更新日時を設定し、
        /// 物理削除要求を論理削除（IsDeleted = true）に変換する内部処理
        /// </summary>
        private void ApplyAuditAndSoftDelete()
        {
            // セッションより現在操作中のアカウント名を取得（無効時は"SYSTEM"）
            var currentUsername = _httpContextAccessor?.HttpContext?.Session.GetString("AccountName") ?? "SYSTEM";
            var now = DateTime.Now;

            var entries = ChangeTracker.Entries();
            foreach (var entry in entries)
            {
                if (entry.Entity is IAuditEntity auditEntity)
                {
                    if (entry.State == EntityState.Added)
                    {
                        // 新規登録時の初期化
                        auditEntity.CreatedBy = currentUsername;
                        auditEntity.CreatedAt = now;
                        auditEntity.UpdatedBy = currentUsername;
                        auditEntity.UpdatedAt = now;
                        auditEntity.IsDeleted = false;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        // 更新時の更新者・更新日時セット
                        auditEntity.UpdatedBy = currentUsername;
                        auditEntity.UpdatedAt = now;
                    }
                    else if (entry.State == EntityState.Deleted)
                    {
                        // 物理削除を論理削除に変更
                        entry.State = EntityState.Modified;
                        auditEntity.IsDeleted = true;
                        auditEntity.UpdatedBy = currentUsername;
                        auditEntity.UpdatedAt = now;
                    }
                }
            }
        }
    }
}
