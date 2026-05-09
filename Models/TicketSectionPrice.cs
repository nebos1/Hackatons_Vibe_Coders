using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventsApp.Models
{
    public class TicketSectionPrice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Ticket))]
        public Guid TicketId { get; set; }

        public Ticket Ticket { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Section))]
        public int SectionId { get; set; }

        public LayoutSection Section { get; set; } = null!;

        [Required]
        [Range(0, 1_000_000)]
        [Column(TypeName = "numeric(18,2)")]
        public decimal Price { get; set; }
    }
}
