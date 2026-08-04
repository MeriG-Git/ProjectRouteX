using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// アカウント（ユーザー）管理エンティティ
    /// ログインアカウント情報や権限ロールを保持します。
    /// </summary>
    [Table("t_account")]
    public class Account : IAuditEntity
    {
        /// <summary>アカウント名（主キー）</summary>
        [Key]
        [Column("account_name", TypeName = "varchar(32)")]
        [Required]
        [StringLength(32)]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>パスワード（SHA256ハッシュ値）</summary>
        [Column("password", TypeName = "varchar(128)")]
        [Required]
        [StringLength(128)]
        public string Password { get; set; } = string.Empty;

        /// <summary>表示名（ユーザー氏名）</summary>
        [Column("display_name", TypeName = "nvarchar(64)")]
        [StringLength(64)]
        public string? DisplayName { get; set; }

        /// <summary>有効フラグ</summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>旧型権限ロール（互換性保持: 0: システム管理者）</summary>
        [Column("role")]
        public int Role { get; set; } = 0;

        /// <summary>割り当てられているロール一覧</summary>
        public virtual System.Collections.Generic.ICollection<AccountRole> AccountRoles { get; set; } = new System.Collections.Generic.List<AccountRole>();

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
