using Microsoft.EntityFrameworkCore;
using GenealogyApp.Models;

namespace GenealogyApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Genealogy> Genealogies { get; set; } = null!;
        public DbSet<GenealogyUser> GenealogyUsers { get; set; } = null!;
        public DbSet<Person> Persons { get; set; } = null!;
        public DbSet<ParentChild> ParentChildren { get; set; } = null!;
        public DbSet<Marriage> Marriages { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ParentChild>()
                .HasIndex(pc => pc.ParentId);
            modelBuilder.Entity<ParentChild>()
                .HasIndex(pc => pc.ChildId);

            modelBuilder.Entity<Person>()
                .HasIndex(p => new { p.GenealogyId, p.GivenName });
        }
    }
}
