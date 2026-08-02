using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 倉庫マスターエンティティ
    /// 自社・提携先の物流拠点倉庫情報を管理します。
    /// </summary>
    [Table("m_warehouse")]
    public class Warehouse : IAuditEntity
    {
        /// <summary>倉庫ID（主キー）</summary>
        [Key]
        [Column("warehouse_id")]
        public Guid WarehouseId { get; set; }

        /// <summary>倉庫名称</summary>
        [Column("warehouse_name", TypeName = "nvarchar(32)")]
        [Required]
        [StringLength(32)]
        public string WarehouseName { get; set; } = string.Empty;

        /// <summary>郵便番号（7桁）</summary>
        [Column("zip_code", TypeName = "varchar(7)")]
        [Required]
        [StringLength(7)]
        public string ZipCode { get; set; } = string.Empty;

        /// <summary>倉庫所在地住所</summary>
        [Column("address", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string Address { get; set; } = string.Empty;

        /// <summary>電話番号</summary>
        [Column("tel", TypeName = "varchar(16)")]
        [Required]
        [StringLength(16)]
        public string Tel { get; set; } = string.Empty;

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
