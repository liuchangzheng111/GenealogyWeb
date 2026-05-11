using System.ComponentModel.DataAnnotations;

namespace GenealogyWeb.Models
{
    /// <summary>
    /// 有向亲子边：Parent → Child，表示血缘上的父母与子女关系（可区分父/母等语义）。
    /// </summary>
    public class ParentChild
    {
        [Key]
        public int Id { get; set; }

        /// <summary>冗余族谱 Id，便于按族谱过滤与索引；须与两端 <see cref="Person"/> 的族谱一致（业务层保证）。</summary>
        public Guid GenealogyId { get; set; }

        public Guid ParentId { get; set; }
        public Guid ChildId { get; set; }

        /// <summary>可选：如 father / mother，供报表或 UI 展示。</summary>
        public string? RelationshipType { get; set; }
    }
}
