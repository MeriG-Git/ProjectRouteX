using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 案件倉庫料金表マスター（荷主＋案件＋倉庫×料金表紐づけ）エンティティ
    /// 各荷主＋案件＋倉庫に対してどの運賃料金表を適用するかを紐付けます。
    /// </summary>
    [Table("t_project_warehouse_freight_table")]
    public class ProjectWarehouseFreightTable : IAuditEntity
    {
        /// <summary>案件ID（複合主キーの一部）</summary>
        [Column("project_id")]
        [Required]
        public Guid ProjectId { get; set; }

        /// <summary>倉庫ID（複合主キーの一部）</summary>
        [Column("warehouse_id")]
        [Required]
        public Guid WarehouseId { get; set; }

        /// <summary>料金表ID（複合主キーの一部）</summary>
        [Column("freight_table_id")]
        [Required]
        public Guid FreightTableId { get; set; }

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

        /// <summary>関連案件オブジェクト</summary>
        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        /// <summary>関連倉庫オブジェクト</summary>
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse? Warehouse { get; set; }

        /// <summary>関連料金表オブジェクト</summary>
        [ForeignKey(nameof(FreightTableId))]
        public virtual FreightTable? FreightTable { get; set; }
    }
}
