using EventsApp.Models;
using EventsApp.ViewModels.Events;
using EventsApp.ViewModels.Posts;
using EventsApp.ViewModels.Tickets;

namespace EventsApp.ViewModels.Account
{
    public class AccountOverviewViewModel
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public string Role { get; set; } = null!;

        public bool HasApplied { get; set; }
        public bool IsApproved { get; set; }
        public bool CanCreatePosts { get; set; }
        public string? OrganizationName { get; set; }
        public DateTime? ApplicationDate { get; set; }

        public int EventsCount { get; set; }
        public int PostsCount { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public int SavedPostsCount { get; set; }
        public int SavedEventsCount { get; set; }
        public int GoingEventsCount { get; set; }

        public bool HasPreferences { get; set; }
        public EventGenre? PreferredGenre { get; set; }
        public string? PreferredCity { get; set; }
        public int? MinAge { get; set; }
        public int? MaxDistanceKm { get; set; }

        public IReadOnlyList<EventCardViewModel> LikedEvents { get; set; } = Array.Empty<EventCardViewModel>();
        public IReadOnlyList<PostCardViewModel> LikedPosts { get; set; } = Array.Empty<PostCardViewModel>();
        public IReadOnlyList<PostCardViewModel> MyPosts { get; set; } = Array.Empty<PostCardViewModel>();
        public IReadOnlyList<PostCardViewModel> SavedPosts { get; set; } = Array.Empty<PostCardViewModel>();
        public IReadOnlyList<EventCardViewModel> SavedEvents { get; set; } = Array.Empty<EventCardViewModel>();

        public int PurchasedTicketsCount { get; set; }
        public IReadOnlyList<MyTicketRowViewModel> RecentTickets { get; set; } = Array.Empty<MyTicketRowViewModel>();

        public UserOnboardingChecklist? OnboardingChecklist { get; set; }
    }

    public class UserOnboardingChecklist
    {
        public bool HasSavedEvent { get; set; }
        public bool HasAttended { get; set; }
        public bool HasFollowed { get; set; }
        public bool HasViewedEvent { get; set; }
        public bool IsComplete => HasSavedEvent && HasAttended && HasFollowed && HasViewedEvent;
    }
}
