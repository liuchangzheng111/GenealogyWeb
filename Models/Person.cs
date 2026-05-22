using System.ComponentModel.DataAnnotations;

namespace GenealogyWeb.Models
{
    /// <summary>族谱内的一名成员（人物）。</summary>
    /// <remarks>姓名仅存 <see cref="GivenName"/>；与 <see cref="Genealogy.Surname"/> 组合显示属 UI 层约定。</remarks>
    public class Person
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>所属族谱。</summary>
        public Guid GenealogyId { get; set; }

        /// <summary>名 / 常用名（模糊查询字段）。</summary>
        public string GivenName { get; set; } = null!;

        /// <summary>性别：示例数据用「男」「女」；统计接口同时接受 M/F。</summary>
        public string? Gender { get; set; }

        public int? BirthYear { get; set; }
        public int? DeathYear { get; set; }

        /// <summary>生平简介。</summary>
        public string? Bio { get; set; }

        /// <summary>
        /// 辈分（代，从 0 起）：无父母为 0；父母为 x 则子女为 x+1。由应用层在增删亲子关系时维护，可用 <see cref="GenerationAssigner.RecalculateGenealogyAsync"/> 全量修复。
        /// </summary>
        public int Generation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
