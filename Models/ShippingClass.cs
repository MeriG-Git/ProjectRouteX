using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 出荷区分マスターエンティティ
    /// 運送会社および運賃区分（個配・路線・チャーター）を管理します。
    /// </summary>
    [Table("m_shipping_class")]
    public class ShippingClass : IAuditEntity
    {
        /// <summary>出荷区分ID（主キー）</summary>
        [Key]
        [Column("shipping_class_id")]
        public Guid ShippingClassId { get; set; }

        /// <summary>運送会社ID</summary>
        [Column("carrier_id")]
        [Required]
        public Guid CarrierId { get; set; }

        /// <summary>出荷区分名称</summary>
        [Column("class_name", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string ClassName { get; set; } = string.Empty;

        /// <summary>運賃表種別（1: 個配, 2: 路線, 3: チャーター）</summary>
        [Column("rate_table_type")]
        [Required]
        public int RateTableType { get; set; } = 1;

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

        /// <summary>関連運送会社オブジェクト</summary>
        [ForeignKey(nameof(CarrierId))]
        public virtual Carrier? Carrier { get; set; }
    }
}
