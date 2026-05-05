using EventsApp.Models;

namespace EventsApp.ViewModels.Posts
{
    public class PostCardViewModel
    {
        public int Id { get; set; }
        public string OrganizerId { get; set; } = null!;
        public int? OrganizerProfileId { get; set; }
        public string OrganizerName { get; set; } = null!;
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int? EventId { get; set; }
        public string? EventTitle { get; set; }
        public string? FirstMediaUrl { get; set; }
        public PostMediaType FirstMediaType { get; set; }
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }
        public int SavesCount { get; set; }
        public bool CurrentUserLiked { get; set; }
        public bool CurrentUserSaved { get; set; }
        public string? AuthorImageUrl { get; set; }
        public bool AuthorIsOrganizer { get; set; }
    }
}
