using System;

namespace RouteXWms.Models
{
    /// <summary>
    /// 自動監査情報（作成者、作成日時、更新者、更新日時）および論理削除機能を持つエンティティ用インターフェース
    /// </summary>
    public interface IAuditEntity : ISoftDeletable
    {
        /// <summary>作成者ユーザー名</summary>
        string? CreatedBy { get; set; }

        /// <summary>作成日時</summary>
        DateTime? CreatedAt { get; set; }

        /// <summary>最終更新者ユーザー名</summary>
        string? UpdatedBy { get; set; }

        /// <summary>最終更新日時</summary>
        DateTime? UpdatedAt { get; set; }
    }
}
