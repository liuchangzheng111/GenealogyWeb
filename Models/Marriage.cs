using System.ComponentModel.DataAnnotations;

namespace GenealogyApp.Models
{
    public class Marriage
    {
        [Key]
        public int Id { get; set; }
        public Guid GenealogyId { get; set; }
        public Guid SpouseAId { get; set; }
        public Guid SpouseBId { get; set; }
        public int? MarriedAtYear { get; set; }
        public int? DivorcedAtYear { get; set; }
        public string? Note { get; set; }
    }
}
