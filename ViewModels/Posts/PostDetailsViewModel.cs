using EventsApp.ViewModels.Social;

namespace EventsApp.ViewModels.Posts
{
    public class PostDetailsViewModel
    {
        public int Id { get; set; }
        public string OrganizerId { get; set; } = null!;
        public int? OrganizerProfileId { get; set; }
        public string OrganizerName { get; set; } = null!;
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? EventId { get; set; }
        public string? EventTitle { get; set; }
        public IReadOnlyList<PostMediaItemViewModel> Media { get; set; } = Array.Empty<PostMediaItemViewModel>();
        public int LikesCount { get; set; }
        public int SavesCount { get; set; }
        public int CommentsCount { get; set; }
        public bool CurrentUserLiked { get; set; }
        public bool CurrentUserSaved { get; set; }
        public string? OrganizerImageUrl { get; set; }
        public bool OrganizerIsOrganizer { get; set; }
        public IReadOnlyList<PostCommentViewModel> Comments { get; set; } = Array.Empty<PostCommentViewModel>();
        public IReadOnlyList<ActingIdentityOptionViewModel> ActingIdentities { get; set; } = Array.Empty<ActingIdentityOptionViewModel>();
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class PostCommentViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string? AuthorImageUrl { get; set; }
        public string AuthorBadgeKey { get; set; } = "identity.user";
        public string AuthorBadgeText { get; set; } = "User";
        public string? AuthorProfileUserId { get; set; }
        public bool IsOrganizerPageAuthor { get; set; }
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public bool CanDelete { get; set; }
        public IReadOnlyList<PostCommentViewModel> Replies { get; set; } = Array.Empty<PostCommentViewModel>();
    }
}
