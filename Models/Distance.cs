using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 距離マスターエンティティ
    /// 運賃表と市区町村コードごとの配送距離（km）を管理します。
    /// </summary>
    [Table("m_distance")]
    public class Distance : IAuditEntity
    {
        /// <summary>運賃表ID（複合主キーの一部）</summary>
        [Column("freight_table_id")]
        [Required]
        public Guid FreightTableId { get; set; }

        /// <summary>市区町村コード（複合主キーの一部）</summary>
        [Column("city_code", TypeName = "varchar(5)")]
        [Required]
        [StringLength(5)]
        public string CityCode { get; set; } = string.Empty;

        /// <summary>配送距離（km）</summary>
        [Column("distance_km")]
        [Required]
        public int DistanceKm { get; set; }

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
