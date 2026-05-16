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
            this.PostCommentLikes = new HashSet<PostCommentLike>();
            this.PostLikes = new HashSet<PostLike>();
            this.PostSaves = new HashSet<PostSave>();
            this.EventComments = new HashSet<EventComment>();
            this.EventCommentLikes = new HashSet<EventCommentLike>();
            this.EventLikes = new HashSet<EventLike>();
            this.EventSaves = new HashSet<EventSave>();
            this.EventAttendances = new HashSet<EventAttendance>();
            this.Followers = new HashSet<Follow>();
            this.Following = new HashSet<Follow>();
            this.SentMessages = new HashSet<Message>();
            this.UserActivities = new HashSet<UserActivity>();
            this.OrganizerProfiles = new HashSet<OrganizerProfile>();
            this.EventSeries = new HashSet<EventSeries>();
            this.VenueLayouts = new HashSet<VenueLayout>();
            this.ProfileSharedEvents = new HashSet<UserProfileSharedEvent>();
            this.BusinessWorkspaces = new HashSet<BusinessWorkspace>();
        }

        [Required]
        public DateTime CreatedAt { get; set; }

        // Refreshed on login + every /api/auth/visit heartbeat from an active
        // tab. Treated together with LastLogoutAt to derive online state for
        // chat: a user counts as online when LastLoginAt is newer than
        // LastLogoutAt AND falls within the heartbeat freshness window.
        public DateTime? LastLoginAt { get; set; }

        // Last time this user had at least one live presence connection (chat hub).
        // Persisted when the final connection drops so peers can show
        // "Active 5 minutes ago" instead of a stale state.
        public DateTime? LastSeenAt { get; set; }

        // Set when the user explicitly signs out, when the last live chat-hub
        // connection drops, or when /api/auth/heartbeat-end fires on tab
        // close. Drives the "Active X ago" label on the chat header.
        public DateTime? LastLogoutAt { get; set; }

        [MaxLength(GlobalConstants.User.FirstNameMaxLength)]
        public string? FirstName { get; set; }

        [MaxLength(GlobalConstants.User.LastNameMaxLength)]
        public string? LastName { get; set; }

        [MaxLength(GlobalConstants.User.ProfileImageUrlMaxLength)]
        public string? ProfileImageUrl { get; set; }

        [MaxLength(GlobalConstants.User.BioMaxLength)]
        public string? Bio { get; set; }

        [MaxLength(GlobalConstants.User.ProfileStatusMaxLength)]
        public string? ProfileStatusText { get; set; }

        [MaxLength(GlobalConstants.User.ProfileStatusEmojiMaxLength)]
        public string? ProfileStatusEmoji { get; set; }

        public DateTime? ProfileStatusUpdatedAt { get; set; }

        public ProfileStatusVisibility ProfileStatusVisibility { get; set; } = ProfileStatusVisibility.Public;

        public int? PinnedEventId { get; set; }

        public Event? PinnedEvent { get; set; }

        public OrganizerData? OrganizerData { get; set; }

        public ICollection<OrganizerProfile> OrganizerProfiles { get; set; }

        public UserPreferences? UserPreferences { get; set; }

        public ICollection<Event> Events { get; set; }

        public ICollection<Post> Posts { get; set; }

        public ICollection<PostComment> PostComments { get; set; }

        public ICollection<PostCommentLike> PostCommentLikes { get; set; }

        public ICollection<PostLike> PostLikes { get; set; }

        public ICollection<PostSave> PostSaves { get; set; }

        public ICollection<EventComment> EventComments { get; set; }

        public ICollection<EventCommentLike> EventCommentLikes { get; set; }

        public ICollection<EventLike> EventLikes { get; set; }

        public ICollection<EventSave> EventSaves { get; set; }

        public ICollection<EventAttendance> EventAttendances { get; set; }

        public ICollection<Follow> Followers { get; set; }

        public ICollection<Follow> Following { get; set; }

        public ICollection<Message> SentMessages { get; set; }

        public ICollection<UserActivity> UserActivities { get; set; }

        public ICollection<EventSeries> EventSeries { get; set; }

        public ICollection<VenueLayout> VenueLayouts { get; set; }

        public ICollection<UserProfileSharedEvent> ProfileSharedEvents { get; set; }

        public ICollection<BusinessWorkspace> BusinessWorkspaces { get; set; }
    }
}
