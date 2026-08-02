namespace RouteXWms.Models
{
    /// <summary>
    /// 論理削除フラグを持つエンティティ用インターフェース
    /// </summary>
    public interface ISoftDeletable
    {
        /// <summary>論理削除フラグ（true: 削除済み, false: 有効）</summary>
        bool IsDeleted { get; set; }
    }
}
