using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// ロール（役割）管理エンティティ
    /// </summary>
    [Table("t_role")]
    public class Role : IAuditEntity
    {
        /// <summary>ロールID（主キー）</summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("role_id")]
        public int RoleId { get; set; }

        /// <summary>ロールコード（ユニークコード 例: SystemAdmin, WarehouseManager）</summary>
        [Column("role_code", TypeName = "varchar(32)")]
        [Required]
        [StringLength(32)]
        public string RoleCode { get; set; } = string.Empty;

        /// <summary>ロール表示名（例: システム管理者）</summary>
        [Column("role_name", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string RoleName { get; set; } = string.Empty;

        /// <summary>説明</summary>
        [Column("description", TypeName = "nvarchar(256)")]
        [StringLength(256)]
        public string? Description { get; set; }

        /// <summary>論理削除フラグ</summary>
        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        /// <summary>作成者</summary>
        [Column("created_by", TypeName = "varchar(64)")]
        public string? CreatedBy { get; set; }

        /// <summary>作成日時</summary>
        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        /// <summary>最終更新者</summary>
        [Column("updated_by", TypeName = "varchar(64)")]
        public string? UpdatedBy { get; set; }

        /// <summary>最終更新日時</summary>
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
