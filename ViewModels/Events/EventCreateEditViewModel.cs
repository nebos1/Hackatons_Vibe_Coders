using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using EventsApp.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EventsApp.ViewModels.Events
{
    public class EventCreateEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(GlobalConstants.Event.TitleMaxLength, MinimumLength = GlobalConstants.Event.TitleMinLength)]
        [Display(Name = "Заглавие")]
        public string Title { get; set; } = null!;

        [StringLength(GlobalConstants.Event.DescriptionMaxLength)]
        [Display(Name = "Описание")]
        public string? Description { get; set; }

        [Required]
        [StringLength(GlobalConstants.Event.CityMaxLength)]
        [Display(Name = "Град")]
        public string City { get; set; } = null!;

        [Required]
        [StringLength(GlobalConstants.Event.AddressMaxLength)]
        [Display(Name = "Адрес")]
        public string Address { get; set; } = null!;

        [Required]
        [Display(Name = "Начало")]
        [DataType(DataType.DateTime)]
        public DateTime StartTime { get; set; } = DateTime.UtcNow.AddDays(1);

        [Required]
        [Display(Name = "Край")]
        [DataType(DataType.DateTime)]
        public DateTime EndTime { get; set; } = DateTime.UtcNow.AddDays(1).AddHours(2);

        [Required]
        [Display(Name = "Жанр")]
        public EventGenre Genre { get; set; }

        [Display(Name = "Публична страница")]
        public int? OrganizerProfileId { get; set; }

        public string? ImageUrl { get; set; }

        [Display(Name = "Качи снимка")]
        public IFormFile? Photo { get; set; }

        [Range(-90, 90)]
        [Display(Name = "Географска ширина")]
        public double? Latitude { get; set; }

        [Range(-180, 180)]
        [Display(Name = "Географска дължина")]
        public double? Longitude { get; set; }

        [Display(Name = "Одобрено")]
        public bool IsApproved { get; set; }

        public bool CanEditApproval { get; set; }

        // Dropdown със списък с градове
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Cities { get; set; } = new();
        // Map city name -> "lat,lng" for client-side bounds
        public Dictionary<string, string> CityCoordinatesMap { get; set; } = new();

        public IEnumerable<SelectListItem> OrganizerProfiles { get; set; } = Array.Empty<SelectListItem>();

        [Display(Name = "График на събитието")]
        public EventRecurrenceType RecurrenceType { get; set; } = EventRecurrenceType.None;

        [Range(1, 365)]
        [Display(Name = "Повтаря се през")]
        public int RecurrenceInterval { get; set; } = 1;

        [Display(Name = "Повтаря се в")]
        public List<DayOfWeek> SelectedDaysOfWeek { get; set; } = new();

        [DataType(DataType.Date)]
        [Display(Name = "Начална дата на серията")]
        public DateTime? RecurrenceStartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Крайна дата на серията")]
        public DateTime? RecurrenceEndDate { get; set; }

        [DataType(DataType.Time)]
        [Display(Name = "Начален час")]
        public TimeSpan? RecurrenceStartTime { get; set; }

        [DataType(DataType.Time)]
        [Display(Name = "Краен час")]
        public TimeSpan? RecurrenceEndTime { get; set; }

        [StringLength(64)]
        public string TimeZone { get; set; } = "Europe/Sofia";

        [Display(Name = "Обхват на редакцията")]
        public RecurringEditScope RecurringEditScope { get; set; } = RecurringEditScope.FutureOccurrences;

        [Display(Name = "Тип билети")]
        public EventTicketingMode TicketingMode { get; set; } = EventTicketingMode.GeneralAdmission;

        [Display(Name = "Преизползваем layout")]
        public int? VenueLayoutId { get; set; }

        public IEnumerable<SelectListItem> VenueLayouts { get; set; } = Array.Empty<SelectListItem>();
    }
}
