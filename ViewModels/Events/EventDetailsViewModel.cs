using EventsApp.Models;
using EventsApp.ViewModels.Tickets;

namespace EventsApp.ViewModels.Events
{
    public class EventDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public EventGenre Genre { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsApproved { get; set; }
        public string Address { get; set; } = null!;
        public string City { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string OrganizerId { get; set; } = null!;
        public string OrganizerName { get; set; } = null!;
        public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();
        public int LikesCount { get; set; }
        public bool CurrentUserLiked { get; set; }
        public IReadOnlyList<EventCommentViewModel> Comments { get; set; } = Array.Empty<EventCommentViewModel>();
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanManageTickets { get; set; }
        public IReadOnlyList<EventTicketOptionViewModel> Tickets { get; set; } = Array.Empty<EventTicketOptionViewModel>();
    }

    public class EventCommentViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public bool CanDelete { get; set; }
    }
}
