using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EventsApp.Common;

namespace EventsApp.Models
{
    public class OrganizerData
    {
        public OrganizerData()
        {
            this.CreatedAt = DateTime.UtcNow;
            this.Approved = false;
            this.VipBoostCreditsAvailable = 1;
        }

        [Key]
        [ForeignKey(nameof(Organizer))]
        public string OrganizerId { get; set; } = null!;

        public ApplicationUser Organizer { get; set; } = null!;

        [Required]
        [MinLength(GlobalConstants.Organizer.OrganizationNameMinLength)]
        [MaxLength(GlobalConstants.Organizer.OrganizationNameMaxLength)]
        public string OrganizationName { get; set; } = null!;

        [MaxLength(GlobalConstants.Organizer.DescriptionMaxLength)]
        public string? Description { get; set; }

        [MaxLength(GlobalConstants.Organizer.PhoneNumberMaxLength)]
        public string? PhoneNumber { get; set; }

        [MaxLength(GlobalConstants.Organizer.CityMaxLength)]
        public string? City { get; set; }

        [MaxLength(80)]
        public string? Country { get; set; }

        [MaxLength(120)]
        public string? ReferralSource { get; set; }

        [MaxLength(GlobalConstants.Organizer.WebsiteMaxLength)]
        public string? Website { get; set; }

        [MaxLength(GlobalConstants.Organizer.CompanyNumberMaxLength)]
        public string? CompanyNumber { get; set; }

        [Required]
        public bool Approved { get; set; }

        [Required]
        public int VipBoostCreditsAvailable { get; set; }

        [Required]
        public int VipBoostCreditsUsed { get; set; }

        [Required]
        public bool FirstApprovalBoostGranted { get; set; }

        [Required]
        public bool FirstApprovalBoostNoticeSeen { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }
    }
}
