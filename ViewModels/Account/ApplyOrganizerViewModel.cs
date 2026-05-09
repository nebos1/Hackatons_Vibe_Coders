using System.ComponentModel.DataAnnotations;
using EventsApp.Common;

namespace EventsApp.ViewModels.Account
{
    public class ApplyOrganizerViewModel
    {
        [Required]
        [StringLength(GlobalConstants.Organizer.OrganizationNameMaxLength, MinimumLength = GlobalConstants.Organizer.OrganizationNameMinLength)]
        [Display(Name = "Име на организация / място")]
        public string OrganizationName { get; set; } = null!;

        [StringLength(GlobalConstants.Organizer.DescriptionMaxLength)]
        [Display(Name = "Описание")]
        public string? Description { get; set; }

        [StringLength(GlobalConstants.Organizer.PhoneNumberMaxLength)]
        [Phone]
        [Display(Name = "Телефон")]
        public string? PhoneNumber { get; set; }

        [StringLength(GlobalConstants.Organizer.CityMaxLength)]
        [Display(Name = "Град")]
        public string? City { get; set; }

        [StringLength(80)]
        [Display(Name = "Страна")]
        public string? Country { get; set; } = "Bulgaria";

        [StringLength(120)]
        [Display(Name = "Как научи за Evento?")]
        public string? ReferralSource { get; set; }

        [StringLength(GlobalConstants.Organizer.WebsiteMaxLength)]
        [Url]
        [Display(Name = "Уебсайт")]
        public string? Website { get; set; }

        [StringLength(GlobalConstants.Organizer.CompanyNumberMaxLength)]
        [Display(Name = "Фирмен номер / ЕИК")]
        public string? CompanyNumber { get; set; }
    }
}
