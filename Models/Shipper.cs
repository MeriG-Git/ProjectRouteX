using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 荷主マスターエンティティ
    /// 物流サービスを利用する荷主企業情報を管理します。
    /// </summary>
    [Table("m_shipper")]
    public class Shipper : IAuditEntity
    {
        /// <summary>荷主ID（主キー）</summary>
        [Key]
        [Column("shipper_id")]
        public Guid ShipperId { get; set; }

        /// <summary>荷主企業名</summary>
        [Column("shipper_name", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string ShipperName { get; set; } = string.Empty;

        /// <summary>荷主住所1</summary>
        [Column("shipper_address1", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string ShipperAddress1 { get; set; } = string.Empty;

        /// <summary>荷主住所2</summary>
        [Column("shipper_address2", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string ShipperAddress2 { get; set; } = string.Empty;

        /// <summary>荷主電話番号</summary>
        [Column("shipper_tel", TypeName = "varchar(16)")]
        [Required]
        [StringLength(16)]
        public string ShipperTel { get; set; } = string.Empty;

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
