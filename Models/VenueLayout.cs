using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventsApp.Models
{
    public enum VenueLayoutStatus
    {
        [Display(Name = "Чернова")]
        Draft = 0,
        [Display(Name = "Активен")]
        Active = 1,
        [Display(Name = "Архивиран")]
        Archived = 2,
    }

    public enum LayoutSectionType
    {
        [Display(Name = "Седящи места")]
        Seated = 0,
        [Display(Name = "Правостоящи")]
        Standing = 1,
        [Display(Name = "VIP")]
        VIP = 2,
        [Display(Name = "Маси")]
        Table = 3,
    }

    public enum SeatType
    {
        [Display(Name = "Стандартно")]
        Standard = 0,
        [Display(Name = "Достъпно")]
        Accessible = 1,
        [Display(Name = "VIP")]
        VIP = 2,
        [Display(Name = "Маса")]
        Table = 3,
    }

    public enum LayoutSeatStatus
    {
        [Display(Name = "Активно")]
        Active = 0,
        [Display(Name = "Блокирано")]
        Blocked = 1,
    }

    public enum EventTicketingMode
    {
        [Display(Name = "Без места")]
        GeneralAdmission = 0,
        [Display(Name = "Схема с места")]
        SeatedLayout = 1,
        [Display(Name = "Правостоящо")]
        Standing = 2,
        [Display(Name = "Маси / VIP зони")]
        TableZones = 3,
    }

    public enum EventSeatInventoryStatus
    {
        [Display(Name = "Свободно")]
        Available = 0,
        [Display(Name = "Резервирано")]
        Reserved = 1,
        [Display(Name = "Продадено")]
        Sold = 2,
        [Display(Name = "Блокирано")]
        Blocked = 3,
    }

    public class VenueLayout
    {
        public VenueLayout()
        {
            this.CreatedAt = DateTime.UtcNow;
            this.UpdatedAt = DateTime.UtcNow;
            this.Version = 1;
            this.Status = VenueLayoutStatus.Active;
            this.Sections = new HashSet<LayoutSection>();
            this.Seats = new HashSet<Seat>();
            this.Events = new HashSet<Event>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Organizer))]
        public string OrganizerId { get; set; } = null!;

        public ApplicationUser Organizer { get; set; } = null!;

        [Required]
        [MaxLength(160)]
        public string VenueName { get; set; } = null!;

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = null!;

        [Range(1, int.MaxValue)]
        public int Version { get; set; }

        [Required]
        public VenueLayoutStatus Status { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }

        public ICollection<LayoutSection> Sections { get; set; }

        public ICollection<Seat> Seats { get; set; }

        public ICollection<Event> Events { get; set; }
    }
}
