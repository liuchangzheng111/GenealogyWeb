using System.ComponentModel.DataAnnotations;

namespace GenealogyApp.Models
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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
