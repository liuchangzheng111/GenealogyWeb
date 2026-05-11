using System.ComponentModel.DataAnnotations;

namespace GenealogyWeb.Models
{
    /// <summary>族谱（一个家族一条记录）：课程字段谱名、姓氏、修谱时间、创建用户。</summary>
    public class Genealogy
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>谱名。</summary>
        public string Title { get; set; } = null!;

        /// <summary>姓氏（可与成员 GivenName 组合展示）。</summary>
        public string Surname { get; set; } = null!;

        /// <summary>修谱时间（业务上可为空）。</summary>
        public DateTime? CompiledAt { get; set; }

        /// <summary>创建者用户 Id；与 <see cref="GenealogyUser"/> 中 Owner 行应对齐。</summary>
        public Guid CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
