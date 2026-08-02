using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 在庫データエンティティ
    /// 倉庫別の商品在庫数、バラ/ケース管理、引当・出荷ステータスを保持します。
    /// </summary>
    [Table("t_inventory")]
    public class Inventory : IAuditEntity
    {
        /// <summary>在庫ID（主キー）</summary>
        [Key]
        [Column("inventory_id")]
        public Guid InventoryId { get; set; }

        /// <summary>元となる入荷ID</summary>
        [Column("inbound_id")]
        [Required]
        public Guid InboundId { get; set; }

        /// <summary>荷主ID</summary>
        [Column("shipper_id")]
        [Required]
        public Guid ShipperId { get; set; }

        /// <summary>保管倉庫ID</summary>
        [Column("warehouse_id")]
        [Required]
        public Guid WarehouseId { get; set; }

        /// <summary>商品コード</summary>
        [Column("product_id", TypeName = "varchar(8)")]
        [Required]
        [StringLength(8)]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>入荷実績日時</summary>
        [Column("actual_inbound_date")]
        public DateTime? ActualInboundDate { get; set; }

        /// <summary>出荷予定日時</summary>
        [Column("scheduled_outbound_date")]
        public DateTime? ScheduledOutboundDate { get; set; }

        /// <summary>出荷実績日時</summary>
        [Column("actual_outbound_date")]
        public DateTime? ActualOutboundDate { get; set; }

        /// <summary>現在保有在庫数量</summary>
        [Column("current_quantity")]
        public int CurrentQuantity { get; set; }

        /// <summary>バラ在庫フラグ（true: バラ在庫, false: ケース在庫）</summary>
        [Column("is_loose")]
        public bool IsLoose { get; set; } = false;

        /// <summary>ステータス（1: 在庫あり, 11: 出荷引当済, 21: 出庫済）</summary>
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

        /// <summary>関連入荷オブジェクト</summary>
        [ForeignKey(nameof(InboundId))]
        public virtual Inbound? Inbound { get; set; }

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
