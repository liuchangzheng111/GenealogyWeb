using System.ComponentModel.DataAnnotations;

namespace GenealogyApp.Models
{
    public class GenealogyUser
    {
        [Key]
        public int Id { get; set; }
        public Guid GenealogyId { get; set; }
        public Guid UserId { get; set; }
        public string Role { get; set; } = "Editor";
        public Guid? InvitedByUserId { get; set; }
        public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    }
}
