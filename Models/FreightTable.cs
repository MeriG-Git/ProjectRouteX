using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 運賃表マスターエンティティ
    /// 運送会社ごとの運賃体系（個配・路線・チャーターなど）を管理します。
    /// </summary>
    [Table("m_freight_table")]
    public class FreightTable : IAuditEntity
    {
        /// <summary>運賃表ID（主キー）</summary>
        [Key]
        [Column("freight_table_id")]
        public Guid FreightTableId { get; set; }

        /// <summary>運賃表名称</summary>
        [Column("rate_name", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string RateName { get; set; } = string.Empty;

        /// <summary>運賃表種別（1: 個配, 2: 路線, 3: チャーター）</summary>
        [Column("rate_table_type")]
        [Required]
        public int RateTableType { get; set; } = 1;

        /// <summary>運送会社ID</summary>
        [Column("carrier_id")]
        [Required]
        public Guid CarrierId { get; set; }

        /// <summary>関連運送会社オブジェクト</summary>
        [ForeignKey(nameof(CarrierId))]
        public virtual Carrier? Carrier { get; set; }

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
    }
}
