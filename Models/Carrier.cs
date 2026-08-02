using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 運送会社マスターエンティティ
    /// 配送を担当する運送会社情報（ヤマト運輸、佐川急便など）を管理します。
    /// </summary>
    [Table("m_carrier")]
    public class Carrier : IAuditEntity
    {
        /// <summary>運送会社ID（主キー）</summary>
        [Key]
        [Column("carrier_id")]
        public Guid CarrierId { get; set; }

        /// <summary>運送会社名</summary>
        [Column("carrier_name", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string CarrierName { get; set; } = string.Empty;

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
