using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using Microsoft.AspNetCore.Http;

namespace EventsApp.ViewModels.Organizer
{
    public class OrganizerProfileViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(GlobalConstants.Organizer.OrganizationNameMaxLength, MinimumLength = GlobalConstants.Organizer.OrganizationNameMinLength)]
        [Display(Name = "Име на организацията")]
        public string OrganizationName { get; set; } = null!;

        [StringLength(GlobalConstants.Organizer.TaglineMaxLength)]
        [Display(Name = "Кратко описание")]
        public string? Tagline { get; set; }

        [StringLength(GlobalConstants.Organizer.DescriptionMaxLength)]
        [Display(Name = "Описание")]
        public string? Description { get; set; }

        [StringLength(GlobalConstants.Organizer.CityMaxLength)]
        [Display(Name = "Град")]
        public string? City { get; set; }

        [Display(Name = "Профилна снимка")]
        public IFormFile? AvatarFile { get; set; }

        [Display(Name = "Корица")]
        public IFormFile? CoverFile { get; set; }

        public string? CurrentAvatarUrl { get; set; }

        public string? CurrentCoverUrl { get; set; }

        [StringLength(GlobalConstants.Organizer.PhoneNumberMaxLength)]
        [Phone]
        [Display(Name = "Телефон")]
        public string? PhoneNumber { get; set; }

        [StringLength(GlobalConstants.Organizer.WebsiteMaxLength)]
        [Url]
        [Display(Name = "Уебсайт")]
        public string? Website { get; set; }

        [StringLength(GlobalConstants.Organizer.ContactEmailMaxLength)]
        [EmailAddress]
        [Display(Name = "Контактен имейл")]
        public string? ContactEmail { get; set; }

        [StringLength(GlobalConstants.Organizer.SocialUrlMaxLength)]
        [Url]
        public string? InstagramUrl { get; set; }

        [StringLength(GlobalConstants.Organizer.SocialUrlMaxLength)]
        [Url]
        public string? FacebookUrl { get; set; }

        [StringLength(GlobalConstants.Organizer.SocialUrlMaxLength)]
        [Url]
        public string? TikTokUrl { get; set; }

        [StringLength(GlobalConstants.Organizer.BrandColorMaxLength)]
        [Display(Name = "Бранд цвят")]
        public string? BrandColor { get; set; }

        [StringLength(GlobalConstants.Organizer.CompanyNumberMaxLength)]
        [Display(Name = "ЕИК / фирмен номер")]
        public string? CompanyNumber { get; set; }

        [Display(Name = "Основна страница")]
        public bool IsDefault { get; set; }

        [Display(Name = "Активна")]
        public bool IsActive { get; set; } = true;

        public bool Approved { get; set; }
    }
}
