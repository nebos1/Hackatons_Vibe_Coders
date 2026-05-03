using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EventsApp.Common;

namespace EventsApp.Models
{
    public enum EventRecurrenceType
    {
        [Display(Name = "Еднократно")]
        None = 0,
        [Display(Name = "Всеки ден")]
        Daily = 1,
        [Display(Name = "Всяка седмица")]
        Weekly = 2,
    }

    public enum EventSeriesStatus
    {
        [Display(Name = "Чернова")]
        Draft = 0,
        [Display(Name = "Публикувана")]
        Published = 1,
        [Display(Name = "Архивирана")]
        Archived = 2,
    }

    public enum EventOccurrenceStatus
    {
        [Display(Name = "Планирана")]
        Scheduled = 0,
        [Display(Name = "Отменена")]
        Cancelled = 1,
        [Display(Name = "Разпродадена")]
        SoldOut = 2,
    }

    public enum RecurringEditScope
    {
        [Display(Name = "Само тази дата")]
        ThisOccurrence = 0,
        [Display(Name = "Всички бъдещи дати")]
        FutureOccurrences = 1,
        [Display(Name = "Цялата серия")]
        EntireSeries = 2,
    }

    public class EventSeries
    {
        public EventSeries()
        {
            this.CreatedAt = DateTime.UtcNow;
            this.UpdatedAt = DateTime.UtcNow;
            this.Interval = 1;
            this.TimeZone = "Europe/Sofia";
            this.Status = EventSeriesStatus.Published;
            this.Occurrences = new HashSet<EventOccurrence>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Event))]
        public int EventId { get; set; }

        public Event Event { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Organizer))]
        public string OrganizerId { get; set; } = null!;

        public ApplicationUser Organizer { get; set; } = null!;

        [Required]
        [MinLength(GlobalConstants.Event.TitleMinLength)]
        [MaxLength(GlobalConstants.Event.TitleMaxLength)]
        public string Title { get; set; } = null!;

        [MaxLength(GlobalConstants.Event.DescriptionMaxLength)]
        public string? Description { get; set; }

        [Required]
        public EventGenre Category { get; set; }

        [Required]
        [MaxLength(GlobalConstants.Event.AddressMaxLength)]
        public string Location { get; set; } = null!;

        [Required]
        [MaxLength(GlobalConstants.Event.CityMaxLength)]
        public string City { get; set; } = null!;

        [MaxLength(GlobalConstants.Event.ImageUrlMaxLength)]
        public string? ImageUrl { get; set; }

        [Required]
        public EventRecurrenceType RecurrenceType { get; set; }

        [Range(1, 365)]
        public int Interval { get; set; }

        [MaxLength(64)]
        public string? DaysOfWeek { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required]
        [MaxLength(64)]
        public string TimeZone { get; set; }

        [Required]
        public EventSeriesStatus Status { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }

        public ICollection<EventOccurrence> Occurrences { get; set; }
    }
}
