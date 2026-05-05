using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventsApp.Models
{
    public class LayoutSection
    {
        public LayoutSection()
        {
            this.Seats = new HashSet<Seat>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(VenueLayout))]
        public int VenueLayoutId { get; set; }

        public VenueLayout VenueLayout { get; set; } = null!;

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = null!;

        [Required]
        [MaxLength(80)]
        public string FloorName { get; set; } = "Floor 1";

        [Required]
        public LayoutSectionType Type { get; set; }

        [MaxLength(32)]
        public string Shape { get; set; } = "Rectangle";

        [Range(0, int.MaxValue)]
        public int Capacity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceModifier { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Rotation { get; set; }

        public ICollection<Seat> Seats { get; set; }
    }
}
