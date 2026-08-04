using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 案件マスターエンティティ
    /// 荷主に紐づく業務案件（プロジェクト）情報を管理します。
    /// </summary>
    [Table("t_project")]
    public class Project : IAuditEntity
    {
        /// <summary>案件ID（主キー）</summary>
        [Key]
        [Column("project_id")]
        public Guid ProjectId { get; set; }

        /// <summary>荷主ID（外部キー）</summary>
        [Column("shipper_id")]
        [Required]
        public Guid ShipperId { get; set; }

        /// <summary>案件名称</summary>
        [Column("project_name", TypeName = "nvarchar(64)")]
        [Required]
        [StringLength(64)]
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>備考</summary>
        [Column("remarks", TypeName = "nvarchar(256)")]
        public string? Remarks { get; set; }

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

        /// <summary>紐づく利用倉庫コレクション</summary>
        public virtual ICollection<ProjectWarehouse> ProjectWarehouses { get; set; } = new List<ProjectWarehouse>();

        /// <summary>紐づく料金表コレクション</summary>
        public virtual ICollection<ProjectWarehouseFreightTable> ProjectWarehouseFreightTables { get; set; } = new List<ProjectWarehouseFreightTable>();
    }
}
