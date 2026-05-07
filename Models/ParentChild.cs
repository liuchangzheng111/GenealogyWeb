using System.ComponentModel.DataAnnotations;

namespace GenealogyApp.Models
{
    public class ParentChild
    {
        [Key]
        public int Id { get; set; }
        public Guid GenealogyId { get; set; }
        public Guid ParentId { get; set; }
        public Guid ChildId { get; set; }
        public string? RelationshipType { get; set; }
    }
}
