using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EventsApp.Common;

namespace EventsApp.Models
{
    public class OrganizerProfile
    {
        public OrganizerProfile()
        {
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
            IsApproved = true;
            Events = new HashSet<Event>();
            Posts = new HashSet<Post>();
        }

        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Running count of past events that have been auto-purged from the DB.
        /// Used so the public organizer page can still display 'X past events'
        /// even after the underlying Event rows are deleted to keep the DB lean.
        /// </summary>
        public int PastEventsCount { get; set; }

        [Required]
        [ForeignKey(nameof(Owner))]
        public string OwnerId { get; set; } = null!;

        public ApplicationUser Owner { get; set; } = null!;

        [ForeignKey(nameof(BusinessWorkspace))]
        public int? BusinessWorkspaceId { get; set; }

        public BusinessWorkspace? BusinessWorkspace { get; set; }

        [Required]
        [MinLength(GlobalConstants.Organizer.OrganizationNameMinLength)]
        [MaxLength(GlobalConstants.Organizer.OrganizationNameMaxLength)]
        public string DisplayName { get; set; } = null!;

        [MaxLength(GlobalConstants.Organizer.TaglineMaxLength)]
        public string? Tagline { get; set; }

        [MaxLength(GlobalConstants.Organizer.DescriptionMaxLength)]
        public string? Description { get; set; }

        [MaxLength(GlobalConstants.Organizer.CityMaxLength)]
        public string? City { get; set; }

        [MaxLength(GlobalConstants.Event.ImageUrlMaxLength)]
        public string? AvatarImageUrl { get; set; }

        [MaxLength(GlobalConstants.Event.ImageUrlMaxLength)]
        public string? CoverImageUrl { get; set; }

        [MaxLength(GlobalConstants.Organizer.WebsiteMaxLength)]
        public string? Website { get; set; }

        [MaxLength(GlobalConstants.Organizer.PhoneNumberMaxLength)]
        public string? PhoneNumber { get; set; }

        [MaxLength(GlobalConstants.Organizer.ContactEmailMaxLength)]
        public string? ContactEmail { get; set; }

        [MaxLength(GlobalConstants.Organizer.SocialUrlMaxLength)]
        public string? InstagramUrl { get; set; }

        [MaxLength(GlobalConstants.Organizer.SocialUrlMaxLength)]
        public string? FacebookUrl { get; set; }

        [MaxLength(GlobalConstants.Organizer.SocialUrlMaxLength)]
        public string? TikTokUrl { get; set; }

        [MaxLength(GlobalConstants.Organizer.BrandColorMaxLength)]
        public string? BrandColor { get; set; }

        public bool IsDefault { get; set; }

        public bool IsDefaultForWorkspace { get; set; }

        public bool IsActive { get; set; }

        public bool IsApproved { get; set; }

        public bool ShowOwnerProfilePublicly { get; set; }

        public bool ShowLegalBusinessNamePublicly { get; set; }

        public BusinessWorkspaceStatus Status { get; set; } = BusinessWorkspaceStatus.Active;

        [Required]
        public DateTime CreatedAt { get; set; }

        public ICollection<Event> Events { get; set; }

        public ICollection<Post> Posts { get; set; }
    }
}
