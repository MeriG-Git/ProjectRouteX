using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RouteXWms.Models
{
    /// <summary>
    /// 出荷指示親エンティティ
    /// CSVファイル取り込み単位での出荷指示メタデータおよび取り込み件数を管理します。
    /// </summary>
    [Table("t_shipping_instruction")]
    public class ShippingInstruction : IAuditEntity
    {
        /// <summary>出荷指示ID（主キー）</summary>
        [Key]
        [Column("shipping_instruction_id")]
        public Guid ShippingInstructionId { get; set; }

        /// <summary>出荷指示グループ識別子（バッチ単位識別コード）</summary>
        [Column("shipping_instruction_group", TypeName = "varchar(64)")]
        [Required]
        [StringLength(64)]
        public string ShippingInstructionGroup { get; set; } = string.Empty;

        /// <summary>取り込みCSVファイル名</summary>
        [Column("file_name", TypeName = "nvarchar(256)")]
        public string? FileName { get; set; }

        /// <summary>ファイルサイズ（バイト）</summary>
        [Column("file_size")]
        public long FileSize { get; set; } = 0;

        /// <summary>荷主ID</summary>
        [Column("shipper_id")]
        [Required]
        public Guid ShipperId { get; set; }

        /// <summary>案件ID</summary>
        [Column("project_id")]
        public Guid? ProjectId { get; set; }

        /// <summary>指定運送会社ID</summary>
        [Column("carrier_id")]
        public Guid? CarrierId { get; set; }

        /// <summary>重量指定区分</summary>
        [Column("weight_spec", TypeName = "varchar(32)")]
        public string? WeightSpec { get; set; }

        /// <summary>取り込み済み明細件数</summary>
        [Column("imported_count")]
        public int ImportedCount { get; set; } = 0;

        /// <summary>ステータス（1: 有効, 999: 取消）</summary>
        [Column("status")]
        public int Status { get; set; } = 1;

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

        /// <summary>関連案件オブジェクト</summary>
        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        /// <summary>関連運送会社オブジェクト</summary>
        [ForeignKey(nameof(CarrierId))]
        public virtual Carrier? Carrier { get; set; }

        /// <summary>紐づく明細出荷データコレクション</summary>
        public virtual ICollection<Outbound> Outbounds { get; set; } = new List<Outbound>();
    }
}
