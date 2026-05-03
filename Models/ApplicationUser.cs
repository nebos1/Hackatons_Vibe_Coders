using System.ComponentModel.DataAnnotations;
using EventsApp.Common;
using Microsoft.AspNetCore.Identity;


namespace EventsApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser()
        {
            this.CreatedAt = DateTime.UtcNow;

            this.Events = new HashSet<Event>();
            this.Posts = new HashSet<Post>();
            this.PostComments = new HashSet<PostComment>();
            this.PostLikes = new HashSet<PostLike>();
            this.PostSaves = new HashSet<PostSave>();
            this.EventComments = new HashSet<EventComment>();
            this.EventLikes = new HashSet<EventLike>();
            this.EventSaves = new HashSet<EventSave>();
            this.EventAttendances = new HashSet<EventAttendance>();
            this.Followers = new HashSet<Follow>();
            this.Following = new HashSet<Follow>();
            this.Stories = new HashSet<Story>();
            this.SentMessages = new HashSet<Message>();
            this.UserActivities = new HashSet<UserActivity>();
            this.OrganizerProfiles = new HashSet<OrganizerProfile>();
            this.EventSeries = new HashSet<EventSeries>();
            this.VenueLayouts = new HashSet<VenueLayout>();
        }

        [Required]
        public DateTime CreatedAt { get; set; }

        [MaxLength(GlobalConstants.User.FirstNameMaxLength)]
        public string? FirstName { get; set; }

        [MaxLength(GlobalConstants.User.LastNameMaxLength)]
        public string? LastName { get; set; }

        [MaxLength(GlobalConstants.User.ProfileImageUrlMaxLength)]
        public string? ProfileImageUrl { get; set; }

        [MaxLength(GlobalConstants.User.BioMaxLength)]
        public string? Bio { get; set; }

        public OrganizerData? OrganizerData { get; set; }

        public ICollection<OrganizerProfile> OrganizerProfiles { get; set; }

        public UserPreferences? UserPreferences { get; set; }

        public ICollection<Event> Events { get; set; }

        public ICollection<Post> Posts { get; set; }

        public ICollection<PostComment> PostComments { get; set; }

        public ICollection<PostLike> PostLikes { get; set; }

        public ICollection<PostSave> PostSaves { get; set; }

        public ICollection<EventComment> EventComments { get; set; }

        public ICollection<EventLike> EventLikes { get; set; }

        public ICollection<EventSave> EventSaves { get; set; }

        public ICollection<EventAttendance> EventAttendances { get; set; }

        public ICollection<Follow> Followers { get; set; }

        public ICollection<Follow> Following { get; set; }

        public ICollection<Story> Stories { get; set; }

        public ICollection<Message> SentMessages { get; set; }

        public ICollection<UserActivity> UserActivities { get; set; }

        public ICollection<EventSeries> EventSeries { get; set; }

        public ICollection<VenueLayout> VenueLayouts { get; set; }
    }
}
