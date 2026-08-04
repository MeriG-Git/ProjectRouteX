using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// ロールとパーミッションの中間マッピングテーブル
    /// </summary>
    [Table("t_role_permission")]
    public class RolePermission
    {
        /// <summary>ロールID（複合主キー・外部キー）</summary>
        [Column("role_id")]
        public int RoleId { get; set; }

        /// <summary>パーミッションID（複合主キー・外部キー）</summary>
        [Column("permission_id")]
        public int PermissionId { get; set; }

        /// <summary>ナビゲーションプロパティ: ロール</summary>
        [ForeignKey("RoleId")]
        public virtual Role? Role { get; set; }

        /// <summary>ナビゲーションプロパティ: パーミッション</summary>
        [ForeignKey("PermissionId")]
        public virtual Permission? Permission { get; set; }
    }
}
