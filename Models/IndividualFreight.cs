using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 個別運賃マスターエンティティ
    /// 都道府県別の個別運賃原価・売価（宅配便などの個配運賃）を管理します。
    /// </summary>
    [Table("m_individual_freight")]
    public class IndividualFreight : IAuditEntity
    {
        /// <summary>個別運賃ID（主キー）</summary>
        [Key]
        [Column("individual_freight_id")]
        public Guid IndividualFreightId { get; set; }

        /// <summary>運賃表ID</summary>
        [Column("freight_table_id")]
        [Required]
        public Guid FreightTableId { get; set; }

        /// <summary>都道府県コード（2桁）</summary>
        [Column("pref_code", TypeName = "varchar(2)")]
        [Required]
        [StringLength(2)]
        public string PrefCode { get; set; } = string.Empty;

        /// <summary>都道府県名</summary>
        [Column("pref_name", TypeName = "nvarchar(32)")]
        [Required]
        [StringLength(32)]
        public string PrefName { get; set; } = string.Empty;

        /// <summary>サイズ区分</summary>
        [Column("size")]
        [Required]
        public int Size { get; set; } = 0;

        /// <summary>重量区分</summary>
        [Column("weight")]
        [Required]
        public int Weight { get; set; } = 0;

        /// <summary>個別運賃原価（円）</summary>
        [Column("cost")]
        [Required]
        public int Cost { get; set; } = 0;

        /// <summary>個別運賃売価（円）</summary>
        [Column("price")]
        [Required]
        public int Price { get; set; } = 0;

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
