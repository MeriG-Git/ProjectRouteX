using System;

namespace RouteXWms.Models
{
    /// <summary>
    /// 出荷指示一覧画面（バッチ単位）表示用ビューモデル
    /// グループごとの出荷指示件数や各種ステータス内訳件数を集計して保持します。
    /// </summary>
    public class ShippingInstructionItemViewModel
    {
        /// <summary>出荷指示ID</summary>
        public Guid ShippingInstructionId { get; set; }

        /// <summary>出荷指示グループ識別コード</summary>
        public string ShippingInstructionGroup { get; set; } = string.Empty;

        /// <summary>取り込みファイル名</summary>
        public string? FileName { get; set; }

        /// <summary>荷主名</summary>
        public string ShipperName { get; set; } = string.Empty;

        /// <summary>案件名</summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>運送会社名</summary>
        public string CarrierName { get; set; } = string.Empty;

        /// <summary>総取り込み件数</summary>
        public int ImportedCount { get; set; }

        /// <summary>登録日時</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>登録ユーザー名</summary>
        public string? CreatedBy { get; set; }

        /// <summary>指示全体ステータス</summary>
        public int Status { get; set; }

        /// <summary>確認中件数（Status = 1）</summary>
        public int PendingCount { get; set; }

        /// <summary>該当運賃なしエラー件数（Status = 801）</summary>
        public int PriceNotFoundCount { get; set; }

        /// <summary>在庫切れエラー件数（Status = 998）</summary>
        public int OutOfStockCount { get; set; }

        /// <summary>出荷確定・引当可能件数（Status = 11以上）</summary>
        public int ConfirmedCount { get; set; }

        /// <summary>取消可能フラグ</summary>
        public bool CanCancel { get; set; }
    }
}
