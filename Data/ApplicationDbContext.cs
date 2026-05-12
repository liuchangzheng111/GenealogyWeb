using Microsoft.EntityFrameworkCore;
using GenealogyWeb.Models;

namespace GenealogyWeb.Data
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

            // --- 外键（Restrict：避免级联误删；应用层已按正确顺序删除） ---
            modelBuilder.Entity<Genealogy>(e =>
            {
                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(g => g.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<GenealogyUser>(e =>
            {
                e.HasOne<Genealogy>()
                    .WithMany()
                    .HasForeignKey(gu => gu.GenealogyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(gu => gu.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(gu => gu.InvitedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Person>(e =>
            {
                e.HasOne<Genealogy>()
                    .WithMany()
                    .HasForeignKey(p => p.GenealogyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(p => new { p.GenealogyId, p.GivenName });

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_Person_BirthBeforeDeath",
                    "`BirthYear` IS NULL OR `DeathYear` IS NULL OR `BirthYear` <= `DeathYear`"));
            });

            modelBuilder.Entity<ParentChild>(e =>
            {
                e.HasIndex(pc => pc.ParentId);
                e.HasIndex(pc => pc.ChildId);
                // 课程「按族谱 + 父查子 / 多代向下」：复合索引优于单列 GenealogyId 左前缀。
                e.HasIndex(pc => new { pc.GenealogyId, pc.ParentId });

                e.HasOne<Genealogy>()
                    .WithMany()
                    .HasForeignKey(pc => pc.GenealogyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne<Person>()
                    .WithMany()
                    .HasForeignKey(pc => pc.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne<Person>()
                    .WithMany()
                    .HasForeignKey(pc => pc.ChildId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.ToTable(t => t.HasCheckConstraint(
                    "CK_ParentChildren_NotSelf",
                    "`ParentId` <> `ChildId`"));
            });

            modelBuilder.Entity<Marriage>(e =>
            {
                e.HasOne<Genealogy>()
                    .WithMany()
                    .HasForeignKey(m => m.GenealogyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne<Person>()
                    .WithMany()
                    .HasForeignKey(m => m.SpouseAId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne<Person>()
                    .WithMany()
                    .HasForeignKey(m => m.SpouseBId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Marriage_DifferentSpouses", "`SpouseAId` <> `SpouseBId`");
                    t.HasCheckConstraint(
                        "CK_Marriage_DivorceAfterWedding",
                        "`DivorcedAtYear` IS NULL OR `MarriedAtYear` IS NULL OR `DivorcedAtYear` >= `MarriedAtYear`");
                });
            });

            // 协作：同一用户在同一族谱仅允许一条成员关系（邀请幂等、防止重复行）。
            modelBuilder.Entity<GenealogyUser>()
                .HasIndex(gu => new { gu.GenealogyId, gu.UserId })
                .IsUnique();
        }
    }
}
