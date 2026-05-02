using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventsApp.Models
{
    public class EventSeatInventory
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Event))]
        public int? EventId { get; set; }

        public Event? Event { get; set; }

        [ForeignKey(nameof(EventOccurrence))]
        public int? EventOccurrenceId { get; set; }

        public EventOccurrence? EventOccurrence { get; set; }

        [Required]
        [ForeignKey(nameof(Seat))]
        public int SeatId { get; set; }

        public Seat Seat { get; set; } = null!;

        [Required]
        public EventSeatInventoryStatus Status { get; set; }

        public DateTime? ReservedUntil { get; set; }

        [ForeignKey(nameof(ReservedByUser))]
        public string? ReservedByUserId { get; set; }

        public ApplicationUser? ReservedByUser { get; set; }

        [ForeignKey(nameof(UserTicket))]
        public Guid? TicketId { get; set; }

        public UserTicket? UserTicket { get; set; }
    }
}
