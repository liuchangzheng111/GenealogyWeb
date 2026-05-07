using System.ComponentModel.DataAnnotations;

namespace GenealogyApp.Models
{
    public class Genealogy
    {
        [Key]
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public DateTime? CompiledAt { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
