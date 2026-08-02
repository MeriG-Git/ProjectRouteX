using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 郵便番号マスターエンティティ
    /// 郵便番号から都道府県コードおよび全国地方公共団体コード（JIS市区町村コード）へのマッピングを保持します。
    /// </summary>
    [Table("m_zip_code")]
    public class ZipCode : IAuditEntity
    {
        /// <summary>郵便番号（主キー, 7桁ハイフンなし）</summary>
        [Key]
        [Column("zip_code", TypeName = "varchar(7)")]
        [Required]
        [StringLength(7)]
        public string ZipCodeValue { get; set; } = string.Empty;

        /// <summary>都道府県コード（2桁）</summary>
        [Column("pref_code", TypeName = "varchar(2)")]
        [Required]
        [StringLength(2)]
        public string PrefCode { get; set; } = string.Empty;

        /// <summary>市区町村コード（5桁）</summary>
        [Column("city_code", TypeName = "varchar(5)")]
        [Required]
        [StringLength(5)]
        public string CityCode { get; set; } = string.Empty;

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
