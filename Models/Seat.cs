using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventsApp.Models
{
    public class Seat
    {
        public Seat()
        {
            this.Status = LayoutSeatStatus.Active;
            this.Inventories = new HashSet<EventSeatInventory>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(VenueLayout))]
        public int VenueLayoutId { get; set; }

        public VenueLayout VenueLayout { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Section))]
        public int SectionId { get; set; }

        public LayoutSection Section { get; set; } = null!;

        [Required]
        [MaxLength(16)]
        public string Row { get; set; } = null!;

        [Required]
        [MaxLength(16)]
        public string Number { get; set; } = null!;

        [MaxLength(48)]
        public string? Label { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Radius { get; set; } = 16;

        public double Rotation { get; set; }

        [Range(1, 100)]
        public int Capacity { get; set; } = 1;

        public bool IsCapacityUnlimited { get; set; }

        [Required]
        public SeatType SeatType { get; set; }

        [Required]
        public LayoutSeatStatus Status { get; set; }

        public ICollection<EventSeatInventory> Inventories { get; set; }
    }
}
