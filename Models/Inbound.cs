using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 入荷データエンティティ
    /// 荷主からの入荷予定・確定情報およびケース数・パレット数を管理します。
    /// </summary>
    [Table("t_inbound")]
    public class Inbound : IAuditEntity
    {
        /// <summary>入荷ID（主キー）</summary>
        [Key]
        [Column("inbound_id")]
        public Guid InboundId { get; set; }

        /// <summary>荷主ID</summary>
        [Column("shipper_id")]
        [Required]
        public Guid ShipperId { get; set; }

        /// <summary>入荷倉庫ID</summary>
        [Column("warehouse_id")]
        [Required]
        public Guid WarehouseId { get; set; }

        /// <summary>商品コード</summary>
        [Column("product_id", TypeName = "varchar(8)")]
        [Required]
        [StringLength(8)]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>入荷予定日</summary>
        [Column("scheduled_date")]
        public DateTime? ScheduledDate { get; set; }

        /// <summary>入荷実績日</summary>
        [Column("actual_date")]
        public DateTime? ActualDate { get; set; }

        /// <summary>入荷確定日</summary>
        [Column("confirmed_date")]
        public DateTime? ConfirmedDate { get; set; }

        /// <summary>入荷種別（1: 路線, 2: パレット, 3: コンテナ）</summary>
        [Column("inbound_type")]
        public int InboundType { get; set; } = 1;

        /// <summary>パレット数</summary>
        [Column("pallet_count")]
        public int PalletCount { get; set; } = 0;

        /// <summary>ケース数</summary>
        [Column("case_count")]
        public int CaseCount { get; set; } = 1;

        /// <summary>備考・注意事項</summary>
        [Column("remarks", TypeName = "nvarchar(500)")]
        public string? Remarks { get; set; }

        /// <summary>ステータス（1: 予定, 11: 確認済, 21: 請求済）</summary>
        [Column("status")]
        public int Status { get; set; } = 1;

        /// <summary>論理削除フラグ</summary>
        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        /// <summary>作成者</summary>
        [Column("created_by", TypeName = "nvarchar(64)")]
        public string? CreatedBy { get; set; }

        /// <summary>作成日時</summary>
        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        /// <summary>最終更新者</summary>
        [Column("updated_by", TypeName = "nvarchar(64)")]
        public string? UpdatedBy { get; set; }

        /// <summary>最終更新日時</summary>
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>関連荷主オブジェクト</summary>
        [ForeignKey(nameof(ShipperId))]
        public virtual Shipper? Shipper { get; set; }

        /// <summary>関連倉庫オブジェクト</summary>
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse? Warehouse { get; set; }

        /// <summary>関連商品オブジェクト</summary>
        [ForeignKey(nameof(ProductId))]
        public virtual Product? Product { get; set; }
    }
}
