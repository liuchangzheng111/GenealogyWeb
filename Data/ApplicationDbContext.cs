using Microsoft.EntityFrameworkCore;
using GenealogyApp.Models;

namespace GenealogyApp.Data
{
    /// <summary>
    /// 应用主数据库上下文。实体与表映射由 EF Core 管理，架构变更请使用 Migrations（勿手改已生成迁移文件中的 Up/Down 除非你知道后果）。
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Genealogy> Genealogies { get; set; } = null!;
        public DbSet<GenealogyUser> GenealogyUsers { get; set; } = null!;
        public DbSet<Person> Persons { get; set; } = null!;
        public DbSet<ParentChild> ParentChildren { get; set; } = null!;
        public DbSet<Marriage> Marriages { get; set; } = null!;

        /// <summary>
        ///  Fluent 配置：索引与唯一约束与课程/查询场景对齐（按父查子、按族谱+姓名模糊等）。
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 血缘：按父或子查邻接表时走索引。
            modelBuilder.Entity<ParentChild>()
                .HasIndex(pc => pc.ParentId);
            modelBuilder.Entity<ParentChild>()
                .HasIndex(pc => pc.ChildId);

            // 成员：同一族谱内按名检索（含 Like）时可部分利用复合索引左前缀。
            modelBuilder.Entity<Person>()
                .HasIndex(p => new { p.GenealogyId, p.GivenName });

            // 协作：同一用户在同一族谱仅允许一条成员关系（邀请幂等、防止重复行）。
            modelBuilder.Entity<GenealogyUser>()
                .HasIndex(gu => new { gu.GenealogyId, gu.UserId })
                .IsUnique();
        }
    }
}
