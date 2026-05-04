using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Posts;

namespace EventsApp.ViewModels.Social
{
    public class PublicProfileViewModel
    {
        public string Id { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string? UserName { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        public bool IsOrganizer { get; set; }
        public string? OrganizationName { get; set; }
        public string? Website { get; set; }
        public string? ProfileStatusText { get; set; }
        public string? ProfileStatusEmoji { get; set; }
        public DateTime? ProfileStatusUpdatedAt { get; set; }
        public ProfileStatusVisibility ProfileStatusVisibility { get; set; }
        public int? PinnedEventId { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public int PostsCount { get; set; }
        public int EventsCount { get; set; }
        public int SavedEventsCount { get; set; }
        public int GoingEventsCount { get; set; }
        public int TicketsCount { get; set; }
        public IReadOnlyList<string> VibeTags { get; set; } = Array.Empty<string>();
        public int EventsAttendedCount { get; set; }
        public int EventsInterestedCount { get; set; }
        public int LikesGivenCount { get; set; }
        public int MonthlyEventsCount { get; set; }
        public int MonthlyNewFollowersCount { get; set; }
        public string? FavouriteGenre { get; set; }
        public int? CitiesVisitedCount { get; set; }
        public bool CurrentUserFollows { get; set; }
        public bool IsCurrentUser { get; set; }
        public bool CanStartConversation { get; set; }
        public bool CanEditProfileStatus { get; set; }
        public EventCardViewModel? PinnedEvent { get; set; }
        public IReadOnlyList<EventCardViewModel> SharedEvents { get; set; } = Array.Empty<EventCardViewModel>();
        public IReadOnlyList<EventCardViewModel> SavedEvents { get; set; } = Array.Empty<EventCardViewModel>();
        public IReadOnlyList<EventCardViewModel> GoingEvents { get; set; } = Array.Empty<EventCardViewModel>();
        public IReadOnlyList<PostCardViewModel> Posts { get; set; } = Array.Empty<PostCardViewModel>();
        public IReadOnlyList<EventCardViewModel> Events { get; set; } = Array.Empty<EventCardViewModel>();
        public IReadOnlyList<MemoryItem> Memories { get; set; } = Array.Empty<MemoryItem>();
    }

    public class MemoryItem
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public string City { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public DateTime EventDate { get; set; }
        public int YearsAgo { get; set; }
    }
}
