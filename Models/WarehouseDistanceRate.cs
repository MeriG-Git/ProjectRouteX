using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 倉庫距離掛率マスター（倉庫×運賃表対応）エンティティ
    /// 各倉庫がどの路線/距離運賃表を適用するかを紐付けます。
    /// </summary>
    [Table("m_warehouse_distance_rate")]
    public class WarehouseDistanceRate : IAuditEntity
    {
        /// <summary>倉庫ID（複合主キーの一部）</summary>
        [Column("warehouse_id")]
        [Required]
        public Guid WarehouseId { get; set; }

        /// <summary>運賃表ID（複合主キーの一部）</summary>
        [Column("freight_table_id")]
        [Required]
        public Guid FreightTableId { get; set; }

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

        /// <summary>関連倉庫オブジェクト</summary>
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse? Warehouse { get; set; }

        /// <summary>関連運賃表オブジェクト</summary>
        [ForeignKey(nameof(FreightTableId))]
        public virtual FreightTable? FreightTable { get; set; }
    }
}
