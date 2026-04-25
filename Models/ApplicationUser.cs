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

            this.Venues = new HashSet<Venue>();
            this.Events = new HashSet<Event>();
            this.Posts = new HashSet<Post>();
            this.PostComments = new HashSet<PostComment>();
            this.PostLikes = new HashSet<PostLike>();
            this.EventComments = new HashSet<EventComment>();
            this.EventLikes = new HashSet<EventLike>();
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

        public UserPreferences? UserPreferences { get; set; }

        public ICollection<Venue> Venues { get; set; }

        public ICollection<Event> Events { get; set; }

        public ICollection<Post> Posts { get; set; }

        public ICollection<PostComment> PostComments { get; set; }

        public ICollection<PostLike> PostLikes { get; set; }

        public ICollection<EventComment> EventComments { get; set; }

        public ICollection<EventLike> EventLikes { get; set; }
    }
}
