using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// パーミッション（機能権限）管理エンティティ
    /// </summary>
    [Table("t_permission")]
    public class Permission : IAuditEntity
    {
        /// <summary>権限ID（主キー）</summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("permission_id")]
        public int PermissionId { get; set; }

        /// <summary>権限コード（例: Master:View, Master:Edit, UserManagement:Manage）</summary>
        [Column("permission_code", TypeName = "varchar(64)")]
        [Required]
        [StringLength(64)]
        public string PermissionCode { get; set; } = string.Empty;

        /// <summary>権限カテゴリ（例: マスター管理, 入荷業務, 出荷業務, システム管理）</summary>
        [Column("category", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string Category { get; set; } = string.Empty;

        /// <summary>権限表示名（例: マスター参照, マスター編集）</summary>
        [Column("permission_name", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string PermissionName { get; set; } = string.Empty;

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
