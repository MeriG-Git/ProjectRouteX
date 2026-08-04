using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// アカウントとロールの中間マッピングテーブル
    /// </summary>
    [Table("t_account_role")]
    public class AccountRole
    {
        /// <summary>アカウント名（複合主キー・外部キー）</summary>
        [Column("account_name", TypeName = "varchar(32)")]
        [Required]
        [StringLength(32)]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>ロールID（複合主キー・外部キー）</summary>
        [Column("role_id")]
        public int RoleId { get; set; }

        /// <summary>ナビゲーションプロパティ: ロール</summary>
        [ForeignKey("RoleId")]
        public virtual Role? Role { get; set; }

        /// <summary>ナビゲーションプロパティ: アカウント</summary>
        [ForeignKey("AccountName")]
        public virtual Account? Account { get; set; }
    }
}
