using EventsApp.Models;

namespace EventsApp.ViewModels.Wrapped
{
    public class WrappedViewModel
    {
        public int Year { get; set; }
        public string DisplayName { get; set; } = null!;
        public int TotalEventsAttended { get; set; }
        public int TotalHoursOnScene { get; set; }
        public int CitiesVisited { get; set; }
        public int OrganizersFollowed { get; set; }
        public int LikesGiven { get; set; }
        public int CommentsPosted { get; set; }
        public string? TopGenre { get; set; }
        public int TopGenreCount { get; set; }
        public string? TopCity { get; set; }
        public int TopCityCount { get; set; }
        public string? TopOrganizer { get; set; }
        public int TopOrganizerCount { get; set; }
        public string? FavouriteEventTitle { get; set; }
        public int? FavouriteEventId { get; set; }
        public string? BusiestMonth { get; set; }
        public int BusiestMonthCount { get; set; }
        public string? AiSummary { get; set; }
        public IReadOnlyList<EventCardSnapshot> TopEvents { get; set; } = Array.Empty<EventCardSnapshot>();
    }

    public class EventCardSnapshot
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string City { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public string? ImageUrl { get; set; }
        public EventGenre Genre { get; set; }
    }
}
