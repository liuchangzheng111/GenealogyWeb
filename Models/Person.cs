using System.ComponentModel.DataAnnotations;

namespace GenealogyApp.Models
{
    public class Person
    {
        [Key]
        public Guid Id { get; set; }
        public Guid GenealogyId { get; set; }
        public string GivenName { get; set; } = null!;
        public string? Gender { get; set; }
        public int? BirthYear { get; set; }
        public int? DeathYear { get; set; }
        public string? Bio { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
