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
        public LayoutSectionType Type { get; set; }

        [Range(0, int.MaxValue)]
        public int Capacity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceModifier { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public ICollection<Seat> Seats { get; set; }
    }
}
