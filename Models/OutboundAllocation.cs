using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 出荷引当明細エンティティ
    /// 出荷データ（Outbound）と具体的な在庫データ（Inventory）の引き当てレコードを管理します。
    /// </summary>
    [Table("t_outbound_allocation")]
    public class OutboundAllocation : IAuditEntity
    {
        /// <summary>引当明細ID（主キー）</summary>
        [Key]
        [Column("allocation_id")]
        public Guid AllocationId { get; set; }

        /// <summary>出荷ID</summary>
        [Column("outbound_id")]
        [Required]
        public Guid OutboundId { get; set; }

        /// <summary>引当先在庫ID</summary>
        [Column("inventory_id")]
        [Required]
        public Guid InventoryId { get; set; }

        /// <summary>引当数量（ケースまたはバラの個数）</summary>
        [Column("allocated_quantity")]
        public int AllocatedQuantity { get; set; }

        /// <summary>バラ出荷フラグ（true: バラ出荷, false: ケース出荷）</summary>
        [Column("is_loose_shipment")]
        public bool IsLooseShipment { get; set; } = false;

        /// <summary>ステータス（11: 引当済, 21: 出庫完了）</summary>
        [Column("status")]
        public int Status { get; set; } = 11;

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

        /// <summary>関連出荷オブジェクト</summary>
        [ForeignKey(nameof(OutboundId))]
        public virtual Outbound? Outbound { get; set; }

        /// <summary>関連在庫オブジェクト</summary>
        [ForeignKey(nameof(InventoryId))]
        public virtual Inventory? Inventory { get; set; }
    }
}
