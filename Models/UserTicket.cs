using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EventsApp.Common;

namespace EventsApp.Models
{
    public class UserTicket
    {
        public UserTicket()
        {
            this.Id = Guid.NewGuid();
            this.CreatedAt = DateTime.UtcNow;
            this.IsUsed = false;
        }

        [Key]
        public Guid Id { get; set; }

        [Required]
        [ForeignKey(nameof(Ticket))]
        public Guid TicketId { get; set; }

        public Ticket Ticket { get; set; } = null!;

        [ForeignKey(nameof(EventOccurrence))]
        public int? EventOccurrenceId { get; set; }

        public EventOccurrence? EventOccurrence { get; set; }

        [ForeignKey(nameof(Seat))]
        public int? SeatId { get; set; }

        public Seat? Seat { get; set; }

        [Required]
        [ForeignKey(nameof(Transaction))]
        public Guid TransactionId { get; set; }

        public Transaction Transaction { get; set; } = null!;

        [Required]
        [StringLength(GlobalConstants.Ticket.QrCodeMaxLength)]
        public string QrCode { get; set; } = null!;

        [Required]
        public bool IsUsed { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UsedAt { get; set; }

        [ForeignKey(nameof(UsedByOrganizer))]
        public string? UsedByOrganizerId { get; set; }

        public ApplicationUser? UsedByOrganizer { get; set; }

        public EventSeatInventory? SeatInventory { get; set; }
    }
}
