using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EventsApp.Common;

namespace EventsApp.Models
{
    public class Event
    {
        public Event()
        {
            this.CreatedAt = DateTime.UtcNow;
            this.IsApproved = false;

            this.Posts = new HashSet<Post>();
            this.Comments = new HashSet<EventComment>();
            this.Likes = new HashSet<EventLike>();
            this.Saves = new HashSet<EventSave>();
            this.Attendances = new HashSet<EventAttendance>();
            this.UserActivities = new HashSet<UserActivity>();
            this.Images = new HashSet<EventImage>();
            this.Tickets = new HashSet<Ticket>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Organizer))]
        public string OrganizerId { get; set; } = null!;

        public ApplicationUser Organizer { get; set; } = null!;

        public int? OrganizerProfileId { get; set; }

        public OrganizerProfile? OrganizerProfile { get; set; }

        [Required]
        [MinLength(GlobalConstants.Event.TitleMinLength)]
        [MaxLength(GlobalConstants.Event.TitleMaxLength)]
        public string Title { get; set; } = null!;

        [MaxLength(GlobalConstants.Event.DescriptionMaxLength)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Required]
        public EventGenre Genre { get; set; }

        [Required]
        [MaxLength(GlobalConstants.Event.AddressMaxLength)]
        public string Address { get; set; } = null!;

        [Required]
        [MaxLength(GlobalConstants.Event.CityMaxLength)]
        public string City { get; set; } = null!;

        [MaxLength(GlobalConstants.Event.ImageUrlMaxLength)]
        public string? ImageUrl { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [Required]
        public bool IsApproved { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public ICollection<Post> Posts { get; set; }

        public ICollection<EventComment> Comments { get; set; }

        public ICollection<EventLike> Likes { get; set; }

        public ICollection<EventSave> Saves { get; set; }

        public ICollection<EventAttendance> Attendances { get; set; }

        public ICollection<UserActivity> UserActivities { get; set; }

        public ICollection<EventImage> Images { get; set; }

        public ICollection<Ticket> Tickets { get; set; }
    }
}
