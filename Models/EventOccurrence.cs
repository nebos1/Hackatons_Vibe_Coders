using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventsApp.Models
{
    public class EventOccurrence
    {
        public EventOccurrence()
        {
            this.Status = EventOccurrenceStatus.Scheduled;
            this.UserTickets = new HashSet<UserTicket>();
            this.SeatInventories = new HashSet<EventSeatInventory>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(EventSeries))]
        public int EventSeriesId { get; set; }

        public EventSeries EventSeries { get; set; } = null!;

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public DateTime EndDateTime { get; set; }

        [Required]
        public EventOccurrenceStatus Status { get; set; }

        public int? CapacityOverride { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PriceOverride { get; set; }

        public ICollection<UserTicket> UserTickets { get; set; }

        public ICollection<EventSeatInventory> SeatInventories { get; set; }
    }
}
