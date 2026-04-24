using System.ComponentModel.DataAnnotations;
using EventsApp.Common;

namespace EventsApp.ViewModels.Account
{
    public class ApplyOrganizerViewModel
    {
        [Required]
        [StringLength(GlobalConstants.Organizer.OrganizationNameMaxLength, MinimumLength = GlobalConstants.Organizer.OrganizationNameMinLength)]
        [Display(Name = "Organization name")]
        public string OrganizationName { get; set; } = null!;

        [StringLength(GlobalConstants.Organizer.DescriptionMaxLength)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(GlobalConstants.Organizer.PhoneNumberMaxLength)]
        [Phone]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [StringLength(GlobalConstants.Organizer.WebsiteMaxLength)]
        [Url]
        [Display(Name = "Website")]
        public string? Website { get; set; }

        [StringLength(GlobalConstants.Organizer.CompanyNumberMaxLength)]
        [Display(Name = "Company / EIK number")]
        public string? CompanyNumber { get; set; }
    }
}
