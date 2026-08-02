using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 出荷データエンティティ
    /// 出荷指示、お届け先住所、配送指定、引当倉庫、最安運賃計算結果、出荷ステータスを管理します。
    /// </summary>
    [Table("t_outbound")]
    public class Outbound : IAuditEntity
    {
        /// <summary>出荷ID（主キー）</summary>
        [Key]
        [Column("outbound_id")]
        public Guid OutboundId { get; set; }

        /// <summary>荷主ID</summary>
        [Column("shipper_id")]
        [Required]
        public Guid ShipperId { get; set; }

        /// <summary>出荷元倉庫ID（最安選定等で割り当てられる倉庫）</summary>
        [Column("warehouse_id")]
        public Guid? WarehouseId { get; set; }

        /// <summary>商品コード</summary>
        [Column("product_id", TypeName = "varchar(8)")]
        [Required]
        [StringLength(8)]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>運送会社ID</summary>
        [Column("carrier_id")]
        [Required]
        public Guid CarrierId { get; set; }

        /// <summary>親出荷指示ID</summary>
        [Column("shipping_instruction_id")]
        public Guid? ShippingInstructionId { get; set; }

        /// <summary>出荷指示グループ識別子（バッチ単位のグループコード）</summary>
        [Column("shipping_instruction_group", TypeName = "varchar(64)")]
        [Required]
        [StringLength(64)]
        public string ShippingInstructionGroup { get; set; } = string.Empty;

        /// <summary>出荷予定日時</summary>
        [Column("scheduled_outbound_date")]
        public DateTime? ScheduledOutboundDate { get; set; }

        /// <summary>出荷実績日時</summary>
        [Column("actual_outbound_date")]
        public DateTime? ActualOutboundDate { get; set; }

        /// <summary>出荷確定日時</summary>
        [Column("confirmed_outbound_date")]
        public DateTime? ConfirmedOutboundDate { get; set; }

        /// <summary>出荷区分ID</summary>
        [Column("shipping_type")]
        [Required]
        public Guid ShippingType { get; set; }

        /// <summary>パレット数</summary>
        [Column("pallet_count")]
        public int PalletCount { get; set; } = 0;

        /// <summary>ケース数</summary>
        [Column("case_count")]
        public int CaseCount { get; set; } = 1;

        /// <summary>パック数/バラ数量</summary>
        [Column("pack_qty")]
        public int? PackQty { get; set; }

        /// <summary>総ピース数（商品単体数換算）</summary>
        [Column("total_pieces")]
        public int TotalPieces { get; set; } = 0;

        /// <summary>出荷総重量（kg）</summary>
        [Column("outbound_weight")]
        public int? OutboundWeight { get; set; }

        /// <summary>計算された最安運賃売価（円）</summary>
        [Column("price", TypeName = "decimal(18,2)")]
        public decimal? Price { get; set; }

        /// <summary>
        /// 出荷ステータス
        /// 1: 確認中, 11: 予定(確定), 21: 送状出力済, 31: 請求済, 801: 該当料金無し, 998: 在庫切れ, 999: 取消
        /// </summary>
        [Column("status")]
        public int Status { get; set; } = 1;

        /// <summary>お届け先コード</summary>
        [Column("recipient_code", TypeName = "varchar(32)")]
        public string? RecipientCode { get; set; }

        /// <summary>お届け先郵便番号（7桁）</summary>
        [Column("zip_code", TypeName = "varchar(7)")]
        public string? ZipCode { get; set; }

        /// <summary>お届け先住所1（都道府県・市区町村）</summary>
        [Column("address1", TypeName = "nvarchar(64)")]
        public string? Address1 { get; set; }

        /// <summary>お届け先住所2（町名・番地）</summary>
        [Column("address2", TypeName = "nvarchar(64)")]
        public string? Address2 { get; set; }

        /// <summary>お届け先住所3（建物名・部屋番号等）</summary>
        [Column("address3", TypeName = "nvarchar(64)")]
        public string? Address3 { get; set; }

        /// <summary>お届け先会社名1</summary>
        [Column("company_name1", TypeName = "nvarchar(64)")]
        public string? CompanyName1 { get; set; }

        /// <summary>お届け先会社名2 / 部署名</summary>
        [Column("company_name2", TypeName = "nvarchar(64)")]
        public string? CompanyName2 { get; set; }

        /// <summary>お届け先受取人氏名</summary>
        [Column("recipient_name", TypeName = "nvarchar(64)")]
        public string? RecipientName { get; set; }

        /// <summary>お届け先電話番号</summary>
        [Column("tel", TypeName = "varchar(16)")]
        public string? Tel { get; set; }

        /// <summary>配送注意事項・備考</summary>
        [Column("notes", TypeName = "nvarchar(500)")]
        public string? Notes { get; set; }

        /// <summary>配達指定日</summary>
        [Column("scheduled_delivery_date")]
        public DateTime? ScheduledDeliveryDate { get; set; }

        /// <summary>納品書アプリ用追加情報1</summary>
        [Column("delivery_note_app1", TypeName = "nvarchar(256)")]
        public string? DeliveryNoteApp1 { get; set; }

        /// <summary>納品書アプリ用追加情報2</summary>
        [Column("delivery_note_app2", TypeName = "nvarchar(256)")]
        public string? DeliveryNoteApp2 { get; set; }

        /// <summary>納品書アプリ用備考</summary>
        [Column("delivery_note_notes", TypeName = "nvarchar(500)")]
        public string? DeliveryNoteNotes { get; set; }

        /// <summary>運送便区分・問合せコード</summary>
        [Column("transport_code", TypeName = "varchar(32)")]
        public string? TransportCode { get; set; }

        /// <summary>社内管理メモ</summary>
        [Column("memo", TypeName = "nvarchar(500)")]
        public string? Memo { get; set; }

        /// <summary>ご請求先コード/発注者コード</summary>
        [Column("sender_code", TypeName = "varchar(12)")]
        [StringLength(12)]
        public string? SenderCode { get; set; }

        /// <summary>配達時間帯指定区分</summary>
        [Column("delivery_time_class")]
        public int? DeliveryTimeClass { get; set; }

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

        /// <summary>親出荷指示オブジェクト</summary>
        [ForeignKey(nameof(ShippingInstructionId))]
        public virtual ShippingInstruction? ShippingInstruction { get; set; }

        /// <summary>関連荷主オブジェクト</summary>
        [ForeignKey(nameof(ShipperId))]
        public virtual Shipper? Shipper { get; set; }

        /// <summary>関連倉庫オブジェクト</summary>
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse? Warehouse { get; set; }

        /// <summary>関連商品オブジェクト</summary>
        [ForeignKey(nameof(ProductId))]
        public virtual Product? Product { get; set; }

        /// <summary>関連運送会社オブジェクト</summary>
        [ForeignKey(nameof(CarrierId))]
        public virtual Carrier? Carrier { get; set; }

        /// <summary>関連出荷区分オブジェクト</summary>
        [ForeignKey(nameof(ShippingType))]
        public virtual ShippingClass? ShippingClass { get; set; }
    }
}
