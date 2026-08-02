using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 商品マスターエンティティ
    /// 商品コード、JANコード、外寸サイズ、重量、ケース入数等の商品属性情報を保持します。
    /// </summary>
    [Table("m_product")]
    public class Product : IAuditEntity
    {
        /// <summary>商品コード（主キー, 8桁）</summary>
        [Key]
        [Column("product_id", TypeName = "varchar(8)")]
        [Required]
        [StringLength(8)]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>商品名</summary>
        [Column("product_name", TypeName = "nvarchar(128)")]
        [Required]
        [StringLength(128)]
        public string ProductName { get; set; } = string.Empty;

        /// <summary>JANコード（13桁）</summary>
        [Column("jan_code", TypeName = "varchar(13)")]
        [Required]
        [StringLength(13)]
        public string JanCode { get; set; } = string.Empty;

        /// <summary>外寸: 縦（cm）</summary>
        [Column("length", TypeName = "decimal(18,2)")]
        public decimal Length { get; set; }

        /// <summary>外寸: 横（cm）</summary>
        [Column("width", TypeName = "decimal(18,2)")]
        public decimal Width { get; set; }

        /// <summary>外寸: 高さ（cm）</summary>
        [Column("height", TypeName = "decimal(18,2)")]
        public decimal Height { get; set; }

        /// <summary>ケース単体重量（kg）</summary>
        [Column("weight")]
        public int Weight { get; set; }

        /// <summary>1ケースあたりの入数（ピース数）</summary>
        [Column("quantity")]
        public int Quantity { get; set; }

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
    }
}
