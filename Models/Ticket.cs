using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EventsApp.Common;

namespace EventsApp.Models
{
    public class Ticket
    {
        public Ticket()
        {
            this.Id = Guid.NewGuid();
            this.CreatedAt = DateTime.UtcNow;
            this.IsActive = true;
            this.UserTickets = new HashSet<UserTicket>();
        }

        [Key]
        public Guid Id { get; set; }

        [Required]
        [ForeignKey(nameof(Event))]
        public int EventId { get; set; }

        public Event Event { get; set; } = null!;

        [Required]
        [StringLength(GlobalConstants.Ticket.NameMaxLength, MinimumLength = GlobalConstants.Ticket.NameMinLength)]
        public string Name { get; set; } = null!;

        [StringLength(GlobalConstants.Ticket.DescriptionMaxLength)]
        public string? Description { get; set; }

        [Required]
        [Range(0, 1_000_000)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityTotal { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityRemaining { get; set; }

        [StringLength(GlobalConstants.Ticket.ImageUrlMaxLength)]
        public string? ImageUrl { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public bool RequiresAttendeeNames { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public ICollection<UserTicket> UserTickets { get; set; }
    }
}
