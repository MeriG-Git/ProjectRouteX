using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 集荷エリアマスターエンティティ
    /// 荷主、出荷区分、出荷先倉庫、運送会社（ヤマト運輸等）の契約・店舗コード情報を対応付けて管理します。
    /// </summary>
    [Table("m_collection_area")]
    public class CollectionArea : IAuditEntity
    {
        /// <summary>集荷エリアID（主キー）</summary>
        [Key]
        [Column("area_id")]
        public Guid AreaId { get; set; }

        /// <summary>荷主ID</summary>
        [Column("shipper_id")]
        [Required]
        public Guid ShipperId { get; set; }

        /// <summary>出荷区分ID</summary>
        [Column("shipping_class_id")]
        [Required]
        public Guid ShippingClassId { get; set; }

        /// <summary>倉庫ID</summary>
        [Column("warehouse_id")]
        [Required]
        public Guid WarehouseId { get; set; }

        /// <summary>集荷エリア名</summary>
        [Column("area_name", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string AreaName { get; set; } = string.Empty;

        /// <summary>送り状種別</summary>
        [Column("invoice_type")]
        public int InvoiceType { get; set; }

        /// <summary>ヤマト店舗コード</summary>
        [Column("yamato_shop_code", TypeName = "varchar(6)")]
        [StringLength(6)]
        public string? YamatoShopCode { get; set; }

        /// <summary>ヤマトお客様コード</summary>
        [Column("yamato_customer_code", TypeName = "varchar(12)")]
        [StringLength(12)]
        public string? YamatoCustomerCode { get; set; }

        /// <summary>ヤマト分類コード/枝番</summary>
        [Column("yamato_sub_code", TypeName = "varchar(3)")]
        [StringLength(3)]
        public string? YamatoSubCode { get; set; }

        /// <summary>ヤマト運賃管理区分</summary>
        [Column("yamato_freight_mgmt")]
        public int YamatoFreightMgmt { get; set; }

        /// <summary>ご請求先コード/発注者コード</summary>
        [Column("sender_code", TypeName = "varchar(12)")]
        [StringLength(12)]
        public string? SenderCode { get; set; }

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

        /// <summary>関連荷主オブジェクト</summary>
        [ForeignKey(nameof(ShipperId))]
        public virtual Shipper? Shipper { get; set; }

        /// <summary>関連出荷区分オブジェクト</summary>
        [ForeignKey(nameof(ShippingClassId))]
        public virtual ShippingClass? ShippingClass { get; set; }

        /// <summary>関連倉庫オブジェクト</summary>
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse? Warehouse { get; set; }
    }
}
