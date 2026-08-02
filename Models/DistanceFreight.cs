using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 距離別運賃マスターエンティティ
    /// 距離帯・荷姿サイズ別の運賃原価および売価金額を定義します。
    /// </summary>
    [Table("m_distance_freight")]
    public class DistanceFreight : IAuditEntity
    {
        /// <summary>運賃ID（主キー）</summary>
        [Key]
        [Column("freight_id")]
        public Guid FreightId { get; set; }

        /// <summary>運賃表ID</summary>
        [Column("freight_table_id")]
        [Required]
        public Guid FreightTableId { get; set; }

        /// <summary>基準距離（km）</summary>
        [Column("distance_km")]
        [Required]
        public int DistanceKm { get; set; }

        /// <summary>サイズ区分（パレット/ケースサイズ等）</summary>
        [Column("size")]
        [Required]
        public int Size { get; set; }

        /// <summary>運賃原価（円）</summary>
        [Column("cost")]
        public int Cost { get; set; }

        /// <summary>運賃売価（円）</summary>
        [Column("price")]
        public int Price { get; set; }

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

        /// <summary>関連運賃表マスターオブジェクト</summary>
        [ForeignKey(nameof(FreightTableId))]
        public virtual FreightTable? FreightTable { get; set; }
    }
}
