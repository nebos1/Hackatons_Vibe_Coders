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
        [Display(Name = "Organization name")]
        public string OrganizationName { get; set; } = null!;

        [StringLength(GlobalConstants.Organizer.TaglineMaxLength)]
        public string? Tagline { get; set; }

        [StringLength(GlobalConstants.Organizer.DescriptionMaxLength)]
        public string? Description { get; set; }

        [StringLength(GlobalConstants.Organizer.CityMaxLength)]
        public string? City { get; set; }

        [Display(Name = "Profile image")]
        public IFormFile? AvatarFile { get; set; }

        [Display(Name = "Cover image")]
        public IFormFile? CoverFile { get; set; }

        public string? CurrentAvatarUrl { get; set; }

        public string? CurrentCoverUrl { get; set; }

        [StringLength(GlobalConstants.Organizer.PhoneNumberMaxLength)]
        [Phone]
        [Display(Name = "Phone")]
        public string? PhoneNumber { get; set; }

        [StringLength(GlobalConstants.Organizer.WebsiteMaxLength)]
        [Url]
        public string? Website { get; set; }

        [StringLength(GlobalConstants.Organizer.ContactEmailMaxLength)]
        [EmailAddress]
        [Display(Name = "Contact email")]
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
        [Display(Name = "Brand color")]
        public string? BrandColor { get; set; }

        [StringLength(GlobalConstants.Organizer.CompanyNumberMaxLength)]
        [Display(Name = "Company number")]
        public string? CompanyNumber { get; set; }

        [Display(Name = "Default page")]
        public bool IsDefault { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public bool Approved { get; set; }
    }
}
